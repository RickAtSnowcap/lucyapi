"""
Google Docs and Drive API client wrapper.
OAuth2 credentials (refresh token) loaded from LucyAPI secrets store on first use.
"""

import logging
import asyncio

from google.oauth2.credentials import Credentials
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError

from .database import get_pool
from .encryption import decrypt
from . import doc_composer

logger = logging.getLogger(__name__)

SCOPES = [
    "https://www.googleapis.com/auth/documents",
    "https://www.googleapis.com/auth/drive.file",
]

# Cached after first initialization
_docs_service = None
_drive_service = None
_folder_id = None


class GoogleApiError(Exception):
    """Wraps Google API errors with a clean message and status code."""
    def __init__(self, message: str, status_code: int = 502):
        super().__init__(message)
        self.status_code = status_code


def _handle_http_error(e: HttpError, context: str = "") -> GoogleApiError:
    """Convert a Google HttpError into a GoogleApiError with appropriate status."""
    status = e.resp.status if hasattr(e, 'resp') else 502
    reason = e._get_reason() if hasattr(e, '_get_reason') else str(e)
    prefix = f"{context}: " if context else ""
    if status == 404:
        return GoogleApiError(f"{prefix}Not found", 404)
    if status == 403:
        return GoogleApiError(f"{prefix}Permission denied", 403)
    if status == 400:
        return GoogleApiError(f"{prefix}{reason}", 400)
    return GoogleApiError(f"{prefix}{reason}", 502)


async def _load_secret(pool, user_id: int, key: str) -> str:
    """Load and decrypt a single secret by key."""
    row = await pool.fetchrow(
        "SELECT encrypted_value FROM secrets WHERE user_id = $1 AND key = $2",
        user_id, key
    )
    if not row:
        raise RuntimeError(f"Secret '{key}' not found")
    return decrypt(row["encrypted_value"])


async def _ensure_initialized(user_id: int):
    """Load OAuth2 credentials and build API clients on first call."""
    global _docs_service, _drive_service, _folder_id
    if _docs_service is not None:
        return

    pool = await get_pool()

    refresh_token = await _load_secret(pool, user_id, "google_oauth_refresh_token")
    client_id = await _load_secret(pool, user_id, "google_oauth_client_id")
    client_secret = await _load_secret(pool, user_id, "google_oauth_client_secret")
    _folder_id = await _load_secret(pool, user_id, "google_docs_folder_id")

    credentials = Credentials(
        token=None,
        refresh_token=refresh_token,
        token_uri="https://oauth2.googleapis.com/token",
        client_id=client_id,
        client_secret=client_secret,
        scopes=SCOPES,
    )

    _docs_service = await asyncio.to_thread(build, "docs", "v1", credentials=credentials)
    _drive_service = await asyncio.to_thread(build, "drive", "v3", credentials=credentials)
    logger.info("Google Docs/Drive clients initialized (OAuth2)")


async def _batch_update(doc_id: str, requests: list[dict]):
    """Execute a batchUpdate on a document."""
    if not requests:
        return
    try:
        await asyncio.to_thread(
            lambda: _docs_service.documents().batchUpdate(
                documentId=doc_id, body={"requests": requests}
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "batchUpdate")


async def _move_to_folder(doc_id: str):
    """Move a document into the target Drive folder."""
    try:
        await asyncio.to_thread(
            lambda: _drive_service.files().update(
                fileId=doc_id,
                addParents=_folder_id,
                removeParents="root",
                fields="id, parents"
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "move to folder")


async def _get_doc_end_index(doc_id: str) -> int:
    """Read a document and return the end index of its body content."""
    try:
        doc = await asyncio.to_thread(
            lambda: _docs_service.documents().get(documentId=doc_id).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "get document")
    body = doc.get("body", {}).get("content", [])
    if body:
        return body[-1].get("endIndex", 1)
    return 1


# ---------------------------------------------------------------------------
# Public API — plain text (Session 1, backward compat)
# ---------------------------------------------------------------------------

async def create_document(user_id: int, title: str, body_text: str = "") -> dict:
    """Create a new Google Doc with optional plain text body, placed in the target folder."""
    await _ensure_initialized(user_id)

    try:
        doc = await asyncio.to_thread(
            lambda: _docs_service.documents().create(body={"title": title}).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "create document")
    doc_id = doc["documentId"]

    await _move_to_folder(doc_id)

    if body_text:
        await _batch_update(doc_id, [
            {"insertText": {"location": {"index": 1}, "text": body_text}}
        ])

    return {
        "document_id": doc_id,
        "title": title,
        "url": f"https://docs.google.com/document/d/{doc_id}/edit",
    }


async def read_document(user_id: int, document_id: str) -> dict:
    """Read a Google Doc and return its plain text content."""
    await _ensure_initialized(user_id)

    try:
        doc = await asyncio.to_thread(
            lambda: _docs_service.documents().get(documentId=document_id).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "read document")

    text = ""
    for element in doc.get("body", {}).get("content", []):
        if "paragraph" in element:
            for elem in element["paragraph"].get("elements", []):
                if "textRun" in elem:
                    text += elem["textRun"]["content"]

    return {
        "document_id": document_id,
        "title": doc.get("title", ""),
        "text": text,
        "url": f"https://docs.google.com/document/d/{document_id}/edit",
    }


# ---------------------------------------------------------------------------
# Public API — formatted documents (Session 2)
# ---------------------------------------------------------------------------

async def create_formatted_document(user_id: int, title: str,
                                    content: list[dict] = None,
                                    branding: str = "none") -> dict:
    """Create a new Google Doc with formatted content from the composition schema."""
    await _ensure_initialized(user_id)

    try:
        doc = await asyncio.to_thread(
            lambda: _docs_service.documents().create(body={"title": title}).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "create document")
    doc_id = doc["documentId"]

    await _move_to_folder(doc_id)

    if content:
        requests = doc_composer.compose(content, branding=branding, start_index=1)
        await _batch_update(doc_id, requests)

    return {
        "document_id": doc_id,
        "title": title,
        "url": f"https://docs.google.com/document/d/{doc_id}/edit",
    }


async def update_document(user_id: int, document_id: str,
                          content: list[dict],
                          branding: str = "none") -> dict:
    """Replace a document's content with new formatted content."""
    await _ensure_initialized(user_id)

    # Get current document end index
    end_index = await _get_doc_end_index(document_id)

    # Delete all existing content (keep the final newline at end_index - 1)
    if end_index > 2:
        await _batch_update(document_id, [{
            "deleteContentRange": {
                "range": {"startIndex": 1, "endIndex": end_index - 1}
            }
        }])

    # Insert new content
    if content:
        requests = doc_composer.compose(content, branding=branding, start_index=1)
        await _batch_update(document_id, requests)

    return {
        "document_id": document_id,
        "url": f"https://docs.google.com/document/d/{document_id}/edit",
    }


async def append_to_document(user_id: int, document_id: str,
                             content: list[dict],
                             branding: str = "none") -> dict:
    """Append formatted content blocks to an existing document."""
    await _ensure_initialized(user_id)

    # Find the current end of content (insert before the final newline)
    end_index = await _get_doc_end_index(document_id)
    start = end_index - 1  # insert before trailing newline

    if content:
        requests = doc_composer.compose(content, branding=branding, start_index=start)
        await _batch_update(document_id, requests)

    return {
        "document_id": document_id,
        "url": f"https://docs.google.com/document/d/{document_id}/edit",
    }


# ---------------------------------------------------------------------------
# Public API — Drive file management (Session 3)
# ---------------------------------------------------------------------------

async def list_files(user_id: int, folder_id: str = None) -> dict:
    """List files in the target folder or a specified subfolder."""
    await _ensure_initialized(user_id)

    parent = folder_id or _folder_id
    try:
        results = await asyncio.to_thread(
            lambda: _drive_service.files().list(
                q=f"'{parent}' in parents and trashed = false",
                fields="files(id, name, mimeType, modifiedTime, webViewLink)",
                orderBy="name",
                pageSize=100,
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "list files")

    return {"folder_id": parent, "files": results.get("files", [])}


async def create_folder(user_id: int, name: str, parent_folder_id: str = None) -> dict:
    """Create a subfolder within the target folder or a specified parent."""
    await _ensure_initialized(user_id)

    parent = parent_folder_id or _folder_id
    metadata = {
        "name": name,
        "mimeType": "application/vnd.google-apps.folder",
        "parents": [parent],
    }
    try:
        folder = await asyncio.to_thread(
            lambda: _drive_service.files().create(
                body=metadata, fields="id, name, webViewLink"
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "create folder")

    return {
        "folder_id": folder["id"],
        "name": folder["name"],
        "url": folder.get("webViewLink", ""),
    }


async def move_file(user_id: int, file_id: str, target_folder_id: str) -> dict:
    """Move a file to a different folder within the shared Drive tree."""
    await _ensure_initialized(user_id)

    try:
        # Get current parents
        file_info = await asyncio.to_thread(
            lambda: _drive_service.files().get(
                fileId=file_id, fields="parents"
            ).execute()
        )
        previous_parents = ",".join(file_info.get("parents", []))

        result = await asyncio.to_thread(
            lambda: _drive_service.files().update(
                fileId=file_id,
                addParents=target_folder_id,
                removeParents=previous_parents,
                fields="id, name, parents",
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "move file")

    return {
        "file_id": result["id"],
        "name": result["name"],
        "parents": result.get("parents", []),
    }


async def delete_file(user_id: int, file_id: str) -> dict:
    """Move a file to trash."""
    await _ensure_initialized(user_id)

    try:
        await asyncio.to_thread(
            lambda: _drive_service.files().update(
                fileId=file_id, body={"trashed": True}
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "delete file")

    return {"file_id": file_id, "trashed": True}


async def get_file_metadata(user_id: int, file_id: str) -> dict:
    """Get file metadata (title, type, dates, URL, parents)."""
    await _ensure_initialized(user_id)

    try:
        result = await asyncio.to_thread(
            lambda: _drive_service.files().get(
                fileId=file_id,
                fields="id, name, mimeType, modifiedTime, createdTime, size, webViewLink, parents",
            ).execute()
        )
    except HttpError as e:
        raise _handle_http_error(e, "get file metadata")

    return result
