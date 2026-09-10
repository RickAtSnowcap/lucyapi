"""
Google Docs and Drive router — documents and file management.
Session 1: plain text create/read.
Session 2: formatted documents via composition engine.
Session 3: Drive file operations (list, create folder, move, delete, metadata).
"""

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel, Field
from typing import Optional

from ..auth import verify_api_key
from .. import google_client
from ..google_client import GoogleApiError

router = APIRouter()


# ---------------------------------------------------------------------------
# Request models
# ---------------------------------------------------------------------------

class CreateDocRequest(BaseModel):
    title: str = Field(..., min_length=1, max_length=500)
    body: Optional[str] = ""
    content: Optional[list[dict]] = None
    branding: Optional[str] = "none"


class UpdateDocRequest(BaseModel):
    content: list[dict] = Field(..., min_length=1)
    branding: Optional[str] = "none"


class AppendDocRequest(BaseModel):
    content: list[dict] = Field(..., min_length=1)
    branding: Optional[str] = "none"


class CreateFolderRequest(BaseModel):
    name: str = Field(..., min_length=1, max_length=500)
    parent_folder_id: Optional[str] = None


class MoveFileRequest(BaseModel):
    target_folder_id: str = Field(..., min_length=1)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _raise_api_error(e: GoogleApiError):
    """Convert a GoogleApiError into an HTTPException."""
    raise HTTPException(status_code=e.status_code, detail=str(e))


# ---------------------------------------------------------------------------
# Document endpoints
# ---------------------------------------------------------------------------

@router.post("/google/docs")
async def create_document(request: CreateDocRequest, caller: dict = Depends(verify_api_key)):
    """Create a new Google Doc. Accepts plain text (body) or formatted content blocks."""
    try:
        if request.content:
            result = await google_client.create_formatted_document(
                caller["user_id"], request.title, request.content, request.branding or "none"
            )
        else:
            result = await google_client.create_document(
                caller["user_id"], request.title, request.body or ""
            )
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.get("/google/docs/{doc_id}")
async def read_document(doc_id: str, caller: dict = Depends(verify_api_key)):
    """Read a Google Doc's plain text content."""
    try:
        result = await google_client.read_document(caller["user_id"], doc_id)
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.put("/google/docs/{doc_id}")
async def update_document(doc_id: str, request: UpdateDocRequest,
                          caller: dict = Depends(verify_api_key)):
    """Replace a document's content with new formatted content."""
    try:
        result = await google_client.update_document(
            caller["user_id"], doc_id, request.content, request.branding or "none"
        )
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.patch("/google/docs/{doc_id}/append")
async def append_to_document(doc_id: str, request: AppendDocRequest,
                             caller: dict = Depends(verify_api_key)):
    """Append formatted content blocks to an existing document."""
    try:
        result = await google_client.append_to_document(
            caller["user_id"], doc_id, request.content, request.branding or "none"
        )
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


# ---------------------------------------------------------------------------
# Drive file management endpoints
# ---------------------------------------------------------------------------

@router.get("/google/drive/files")
async def list_files(folder_id: Optional[str] = Query(None, description="Subfolder ID (defaults to root shared folder)"),
                     caller: dict = Depends(verify_api_key)):
    """List files in the target folder or a specified subfolder."""
    try:
        result = await google_client.list_files(caller["user_id"], folder_id)
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.post("/google/drive/folders")
async def create_folder(request: CreateFolderRequest, caller: dict = Depends(verify_api_key)):
    """Create a subfolder within the target folder."""
    try:
        result = await google_client.create_folder(
            caller["user_id"], request.name, request.parent_folder_id
        )
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.put("/google/drive/files/{file_id}/move")
async def move_file(file_id: str, request: MoveFileRequest,
                    caller: dict = Depends(verify_api_key)):
    """Move a file to a different folder."""
    try:
        result = await google_client.move_file(
            caller["user_id"], file_id, request.target_folder_id
        )
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.delete("/google/drive/files/{file_id}")
async def delete_file(file_id: str, caller: dict = Depends(verify_api_key)):
    """Move a file to trash."""
    try:
        result = await google_client.delete_file(caller["user_id"], file_id)
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))


@router.get("/google/drive/files/{file_id}/meta")
async def get_file_metadata(file_id: str, caller: dict = Depends(verify_api_key)):
    """Get file metadata (title, type, dates, URL)."""
    try:
        result = await google_client.get_file_metadata(caller["user_id"], file_id)
        return result
    except GoogleApiError as e:
        _raise_api_error(e)
    except Exception as e:
        raise HTTPException(status_code=502, detail=str(e))
