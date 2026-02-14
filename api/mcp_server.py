"""
MCP Streamable HTTP server for LucyAPI.

Exposes all LucyAPI endpoints as MCP tools so that Claude custom connectors
(including mobile) can interact with the full API.

Authentication: Each agent passes its own agent_key parameter on every tool
call. The MCP server resolves caller identity from that key. No key = rejected.
"""

import json
import os
import logging
from datetime import datetime, timezone
from typing import Any
from zoneinfo import ZoneInfo

from mcp import types
from mcp.server.lowlevel import Server
from mcp.server.streamable_http_manager import StreamableHTTPSessionManager

from .database import get_pool
from .encryption import encrypt, decrypt
from . import google_client

logger = logging.getLogger(__name__)

TIMEZONE = ZoneInfo("America/Denver")

# No default API key — agents must authenticate explicitly via agent_key parameter

BASE_URL = "https://lucyapi.snowcapsystems.com"


def _browse_url(path: str, api_key: str) -> str:
    """Build a browser-ready URL with auth for a given API path."""
    return f"{BASE_URL}{path}?agent_key={api_key}"


async def _get_caller(api_key: str | None = None) -> dict:
    """Resolve agent context from an API key. Requires explicit key."""
    if not api_key:
        raise ValueError("agent_key is required for authentication")
    key = api_key
    pool = await get_pool()
    row = await pool.fetchrow(
        """
        SELECT a.agent_id, a.name AS agent_name, a.user_id, u.name AS user_name
        FROM agents a JOIN users u ON a.user_id = u.user_id
        WHERE a.api_key = $1
        """,
        key,
    )
    if not row:
        raise ValueError("Invalid agent_key — agent not found")
    return dict(row)


async def _get_agent(agent_name: str, caller: dict) -> dict | None:
    """Look up an agent by name, verify same-user access."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        return None
    return dict(agent)


def _json_serial(obj: Any) -> str:
    if isinstance(obj, datetime):
        return obj.isoformat()
    raise TypeError(f"Type {type(obj)} not serializable")


def _to_json(data: Any) -> str:
    return json.dumps(data, default=_json_serial, indent=2)


def _tool(name, desc, props=None, req=None):
    # agent_key is always required — ensure it's in every tool's required list
    required = list(req or [])
    if "agent_key" not in required:
        required.insert(0, "agent_key")
    return types.Tool(
        name=name, description=desc,
        inputSchema={"type": "object", "properties": props or {}, "required": required},
    )

# Reusable schema fragments
_K = {"agent_key": {"type": "string", "description": "Your agent API key for authentication"}}
_A = {"agent_name": {"type": "string", "description": "Agent name (e.g. 'lucy')"}}
_ID = {"pkid": {"type": "integer", "description": "Node/item ID"}}
_T = {"title": {"type": "string", "description": "Title"}}
_D = {"description": {"type": "string", "description": "Description"}}
_PAR = {"parent_id": {"type": "integer", "description": "Parent node ID (0 for root)", "default": 0}}
_PJ = {"project_id": {"type": "integer", "description": "Project ID"}}
_SC = {"section_id": {"type": "integer", "description": "Section ID"}}
_FP = {"file_path": {"type": "string", "description": "Associated file path"}}
_WK = {"wiki_id": {"type": "integer", "description": "Wiki ID"}}
_WS = {"section_id": {"type": "integer", "description": "Section ID"}}
_TAGS = {"tags": {"type": "array", "items": {"type": "string"}, "description": "Tags for the section"}}


def create_mcp_server() -> Server:
    app = Server("lucyapi-mcp")

    @app.list_tools()
    async def list_tools() -> list[types.Tool]:
        return [
            _tool("get_time", "Get current UTC and Mountain Time timestamps, timezone, and day of week.", {**_K}),
            _tool("get_context", "Full context payload: always_load titles, memory titles, preferences manifest, project manifest.", {**_K, **_A}, ["agent_name"]),
            _tool("get_always_load", "Full always-load tree with descriptions.", {**_K, **_A}, ["agent_name"]),
            _tool("get_always_load_item", "Single always-load node and its children.", {**_K, **_A, **_ID}, ["agent_name", "pkid"]),
            _tool("create_always_load", "Create an always_load node.", {**_K, **_A, **_T, **_D, **_PAR}, ["agent_name", "title"]),
            _tool("update_always_load", "Update an always_load node.", {**_K, **_A, **_ID, **_T, **_D}, ["agent_name", "pkid"]),
            _tool("delete_always_load", "Delete an always_load node and children.", {**_K, **_A, **_ID}, ["agent_name", "pkid"]),
            _tool("get_memories", "All memories with full descriptions.", {**_K, **_A}, ["agent_name"]),
            _tool("get_memory", "Single memory by ID.", {**_K, **_A, **_ID}, ["agent_name", "pkid"]),
            _tool("create_memory", "Create a new memory.", {**_K, **_A, **_T, **_D}, ["agent_name", "title"]),
            _tool("update_memory", "Update a memory.", {**_K, **_A, **_ID, **_T, **_D}, ["agent_name", "pkid"]),
            _tool("delete_memory", "Delete a memory.", {**_K, **_A, **_ID}, ["agent_name", "pkid"]),
            _tool("get_preferences", "Top-level preference categories.", {**_K, **_A}, ["agent_name"]),
            _tool("get_preference", "Preference node and its children.", {**_K, **_A, **_ID}, ["agent_name", "pkid"]),
            _tool("create_preference", "Create a preference node.", {**_K, **_A, **_T, **_D, **_PAR}, ["agent_name", "title"]),
            _tool("update_preference", "Update a preference node.", {**_K, **_A, **_ID, **_T, **_D}, ["agent_name", "pkid"]),
            _tool("delete_preference", "Delete a preference node and children.", {**_K, **_A, **_ID}, ["agent_name", "pkid"]),
            _tool("get_projects", "All projects for the user.", {**_K}),
            _tool("get_project", "Project header with section tree.", {**_K, **_PJ}, ["project_id"]),
            _tool("get_section", "Project section and its children.", {**_K, **_PJ, **_SC}, ["project_id", "section_id"]),
            _tool("create_project", "Create a new project.", {**_K, **_T, **_D}, ["title"]),
            _tool("create_section", "Create a section under a project.", {**_K, **_PJ, **_T, **_D, **_PAR, **_FP}, ["project_id", "title"]),
            _tool("update_project", "Update a project.", {**_K, **_PJ, **_T, **_D}, ["project_id"]),
            _tool("update_section", "Update a project section.", {**_K, **_PJ, **_SC, **_T, **_D, **_FP}, ["project_id", "section_id"]),
            _tool("delete_project", "Delete a project and all sections.", {**_K, **_PJ}, ["project_id"]),
            _tool("delete_section", "Delete a section and descendants.", {**_K, **_PJ, **_SC}, ["project_id", "section_id"]),
            # Hints (user-scoped)
            _tool("get_hints", "Full hints tree with descriptions.", {**_K}),
            _tool("get_hint", "Hint node and its children.", {**_K, "hint_id": {"type": "integer", "description": "Hint ID"}}, ["hint_id"]),
            _tool("create_hint", "Create a hint node.", {**_K, **_T, **_D, **_PAR}, ["title"]),
            _tool("update_hint", "Update a hint node.", {**_K, "hint_id": {"type": "integer", "description": "Hint ID"}, **_T, **_D}, ["hint_id"]),
            _tool("delete_hint", "Delete a hint node and children.", {**_K, "hint_id": {"type": "integer", "description": "Hint ID"}}, ["hint_id"]),
            # Wikis (user-scoped)
            _tool("get_wikis", "All wikis for the user.", {**_K}),
            _tool("get_wiki", "Wiki header with section tree (includes tags and updated_at).", {**_K, **_WK}, ["wiki_id"]),
            _tool("create_wiki", "Create a new wiki.", {**_K, **_T, **_D}, ["title"]),
            _tool("update_wiki", "Update a wiki.", {**_K, **_WK, **_T, **_D}, ["wiki_id"]),
            _tool("delete_wiki", "Delete a wiki and all sections/tags.", {**_K, **_WK}, ["wiki_id"]),
            _tool("create_wiki_section", "Create a section under a wiki.", {**_K, **_WK, **_T, **_D, **_PAR, **_TAGS}, ["wiki_id", "title"]),
            _tool("get_wiki_section", "Wiki section with children and tags.", {**_K, **_WK, **_WS}, ["wiki_id", "section_id"]),
            _tool("update_wiki_section", "Update a wiki section (including tags).", {**_K, **_WK, **_WS, **_T, **_D, **_TAGS}, ["wiki_id", "section_id"]),
            _tool("delete_wiki_section", "Delete a wiki section and descendants.", {**_K, **_WK, **_WS}, ["wiki_id", "section_id"]),
            _tool("get_wiki_tags", "List all unique tags in a wiki.", {**_K, **_WK}, ["wiki_id"]),
            _tool("search_wiki_tag", "Find all sections across wikis matching a tag.", {**_K, "tag": {"type": "string", "description": "Tag to search for"}}, ["tag"]),
            # Sharing (user-scoped)
            _tool("share_object", "Share a project, hint category, or wiki with another user.", {
                **_K,
                "shared_to_user_id": {"type": "integer", "description": "User ID to share with"},
                "object_type_id": {"type": "integer", "description": "1=project, 2=hint, 3=wiki"},
                "object_id": {"type": "integer", "description": "ID of the object to share"},
                "permission_level": {"type": "integer", "description": "1=read, 2=read+edit, 3=full control", "default": 1}
            }, ["shared_to_user_id", "object_type_id", "object_id"]),
            _tool("revoke_share", "Revoke a previously shared object.", {
                **_K,
                "share_id": {"type": "integer", "description": "Share ID to revoke"}
            }, ["share_id"]),
            _tool("get_shared_by_me", "List all objects you have shared with other users.", {**_K}),
            _tool("get_shared_to_me", "List all objects other users have shared with you.", {**_K}),
            _tool("create_session", "Log a session start.", {**_K, "project": {"type": "string", "description": "Optional project context"}}, []),
            _tool("get_last_session", "Most recent session for the calling agent.", {**_K}),
            _tool("save_notes", "Email markdown content as an attachment to Rick.", {**_K, "subject": {"type": "string", "description": "Email subject"}, "content": {"type": "string", "description": "Markdown content"}}, ["subject", "content"]),
            # Secrets (user-scoped)
            _tool("list_secrets", "List all secret key names (no values).", {**_K}),
            _tool("get_secret", "Get decrypted secret value.", {**_K, "key": {"type": "string", "description": "Secret key name"}}, ["key"]),
            _tool("set_secret", "Create or update an encrypted secret.", {**_K, "key": {"type": "string", "description": "Secret key name"}, "value": {"type": "string", "description": "Secret value (will be encrypted)"}}, ["key", "value"]),
            _tool("delete_secret", "Delete a secret.", {**_K, "key": {"type": "string", "description": "Secret key name"}}, ["key"]),
            # Handoffs (agent-scoped, cross-agent read/create)
            _tool("list_handoffs", "List pending handoffs for an agent.", {**_K, **_A}, ["agent_name"]),
            _tool("get_handoff", "Get a specific handoff.", {**_K, **_A, "handoff_id": {"type": "integer", "description": "Handoff ID"}}, ["agent_name", "handoff_id"]),
            _tool("create_handoff", "Create a handoff prompt for an agent (cross-agent OK).", {**_K, **_A, **_T, "prompt": {"type": "string", "description": "Handoff prompt text"}}, ["agent_name", "title", "prompt"]),
            _tool("pickup_handoff", "Mark a handoff as picked up (named agent only).", {**_K, **_A, "handoff_id": {"type": "integer", "description": "Handoff ID"}}, ["agent_name", "handoff_id"]),
            _tool("delete_handoff", "Delete a handoff (named agent only).", {**_K, **_A, "handoff_id": {"type": "integer", "description": "Handoff ID"}}, ["agent_name", "handoff_id"]),
            # Images (Gemini-powered generation, editing, analysis)
            _tool("generate_image", "Generate an image from a text prompt via Gemini.", {**_K, "prompt": {"type": "string", "description": "Text prompt for image generation"}, "model": {"type": "string", "description": "Model alias (nano-banana or nano-banana-pro)", "default": "nano-banana"}, "aspect_ratio": {"type": "string", "description": "Aspect ratio (1:1, 16:9, etc.)", "default": "1:1"}}, ["prompt"]),
            _tool("edit_image", "Edit an existing image with a text prompt via Gemini.", {**_K, "prompt": {"type": "string", "description": "Edit instruction"}, "image_id": {"type": "integer", "description": "Source image ID (provide this or image_url)"}, "image_url": {"type": "string", "description": "Source image URL (provide this or image_id)"}, "model": {"type": "string", "description": "Model alias", "default": "nano-banana"}}, ["prompt"]),
            _tool("analyze_image", "Analyze an image and return a text description via Gemini.", {**_K, "image_id": {"type": "integer", "description": "Image ID (provide this or image_url)"}, "image_url": {"type": "string", "description": "Image URL (provide this or image_id)"}, "prompt": {"type": "string", "description": "Analysis prompt", "default": "Describe this image in detail"}}, []),
            _tool("list_images", "List generated images with optional keep filter.", {**_K, "keep": {"type": "boolean", "description": "Filter by keep flag"}, "limit": {"type": "integer", "description": "Max results (default 50)", "default": 50}}, []),
            _tool("keep_image", "Mark an image as keep=true to protect from cleanup.", {**_K, "image_id": {"type": "integer", "description": "Image ID to keep"}}, ["image_id"]),
            _tool("delete_image", "Delete an image file and DB row.", {**_K, "image_id": {"type": "integer", "description": "Image ID to delete"}, "force": {"type": "boolean", "description": "Force delete even if keep=true", "default": False}}, ["image_id"]),
            _tool("cleanup_images", "Bulk delete all images where keep=false.", {**_K}),
            # Google Docs (user-scoped via OAuth2)
            _tool("create_google_doc", "Create a new Google Doc in the shared Drive folder.", {**_K, "title": {"type": "string", "description": "Document title"}, "body": {"type": "string", "description": "Plain text body content"}, "content": {"type": "array", "items": {"type": "object"}, "description": "Array of content blocks (heading, paragraph, list, table, page_break, image). When provided, body is ignored."}, "branding": {"type": "string", "description": "Branding preset: snowcap or none (default: none)"}}, ["title"]),
            _tool("read_google_doc", "Read a Google Doc and return its plain text content.", {**_K, "doc_id": {"type": "string", "description": "Google Doc document ID"}}, ["doc_id"]),
            _tool("update_google_doc", "Replace a document's content with new formatted content.", {**_K, "doc_id": {"type": "string", "description": "Google Doc document ID"}, "content": {"type": "array", "items": {"type": "object"}, "description": "Array of content blocks"}, "branding": {"type": "string", "description": "Branding preset: snowcap or none (default: none)"}}, ["doc_id", "content"]),
            _tool("append_google_doc", "Append formatted content blocks to an existing document.", {**_K, "doc_id": {"type": "string", "description": "Google Doc document ID"}, "content": {"type": "array", "items": {"type": "object"}, "description": "Array of content blocks to append"}, "branding": {"type": "string", "description": "Branding preset: snowcap or none (default: none)"}}, ["doc_id", "content"]),
            # Google Drive file management (user-scoped via OAuth2)
            _tool("list_google_files", "List files in the shared Drive folder or a subfolder.", {**_K, "folder_id": {"type": "string", "description": "Subfolder ID (defaults to root shared folder)"}}, []),
            _tool("create_google_folder", "Create a subfolder in the shared Drive folder.", {**_K, "name": {"type": "string", "description": "Folder name"}, "parent_folder_id": {"type": "string", "description": "Parent folder ID (defaults to root shared folder)"}}, ["name"]),
            _tool("move_google_file", "Move a file to a different folder.", {**_K, "file_id": {"type": "string", "description": "File ID to move"}, "target_folder_id": {"type": "string", "description": "Destination folder ID"}}, ["file_id", "target_folder_id"]),
            _tool("delete_google_file", "Move a file to trash.", {**_K, "file_id": {"type": "string", "description": "File ID to delete"}}, ["file_id"]),
            _tool("get_google_file_meta", "Get file metadata (title, type, dates, URL).", {**_K, "file_id": {"type": "string", "description": "File ID"}}, ["file_id"]),
        ]

    @app.call_tool()
    async def call_tool(name: str, arguments: dict[str, Any]) -> list[types.ContentBlock]:
        try:
            result = await _dispatch(name, arguments)
            return [types.TextContent(type="text", text=_to_json(result))]
        except Exception as e:
            logger.error(f"MCP tool '{name}' failed: {e}")
            return [types.TextContent(type="text", text=_to_json({"error": str(e)}))]

    return app


async def _dispatch(name: str, args: dict[str, Any]) -> Any:
    pool = await get_pool()
    agent_key = args.pop("agent_key", None)
    caller = await _get_caller(agent_key)

    # ── System ────────────────────────────────────────────────────
    if name == "get_time":
        now_utc = datetime.now(ZoneInfo("UTC"))
        now_local = now_utc.astimezone(TIMEZONE)
        return {"utc": now_utc.isoformat(), "local": now_local.isoformat(), "tz": "America/Denver", "day": now_local.strftime("%A")}

    # ── Context ───────────────────────────────────────────────────
    if name == "get_context":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        al = await pool.fetch("SELECT pkid, parent_id, title FROM always_load WHERE agent_id = $1 ORDER BY parent_id, pkid", agent["agent_id"])
        mem = await pool.fetch("SELECT pkid, title FROM memories WHERE agent_id = $1 ORDER BY pkid", agent["agent_id"])
        pref = await pool.fetch("SELECT pkid, title FROM preferences WHERE agent_id = $1 AND parent_id = 0 ORDER BY pkid", agent["agent_id"])
        proj = await pool.fetch("SELECT project_id, title FROM projects WHERE user_id = $1 ORDER BY project_id", caller["user_id"])
        return {"agent": args["agent_name"], "always_load": [dict(r) for r in al], "memories": [dict(r) for r in mem], "preferences_manifest": [dict(r) for r in pref], "projects_manifest": [dict(r) for r in proj]}

    # ── Always Load ───────────────────────────────────────────────
    if name == "get_always_load":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        rows = await pool.fetch("SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 ORDER BY parent_id, pkid", agent["agent_id"])
        return {"agent": args["agent_name"], "always_load": [dict(r) for r in rows]}

    if name == "get_always_load_item":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        node = await pool.fetchrow("SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 AND pkid = $2", agent["agent_id"], args["pkid"])
        children = await pool.fetch("SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 AND parent_id = $2 ORDER BY pkid", agent["agent_id"], args["pkid"])
        return {"node": dict(node) if node else None, "children": [dict(r) for r in children]}

    if name == "create_always_load":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may write to its always_load"}
        row = await pool.fetchrow("INSERT INTO always_load (agent_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING pkid, parent_id, title", agent["agent_id"], args.get("parent_id", 0), args["title"], args.get("description"))
        return {"created": dict(row)}

    if name == "update_always_load":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may modify its always_load"}
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        updates.append("updated_at = NOW()")
        values.extend([agent["agent_id"], args["pkid"]])
        row = await pool.fetchrow(f"UPDATE always_load SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title", *values)
        return {"updated": dict(row)} if row else {"error": "Node not found"}

    if name == "delete_always_load":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may delete its always_load"}
        result = await pool.execute("WITH RECURSIVE subtree AS (SELECT pkid FROM always_load WHERE agent_id = $1 AND pkid = $2 UNION ALL SELECT a.pkid FROM always_load a INNER JOIN subtree s ON a.parent_id = s.pkid WHERE a.agent_id = $1) DELETE FROM always_load WHERE pkid IN (SELECT pkid FROM subtree)", agent["agent_id"], args["pkid"])
        count = int(result.split()[-1])
        return {"deleted": args["pkid"], "descendants_deleted": count - 1} if count > 0 else {"error": "Node not found"}

    # ── Memories ──────────────────────────────────────────────────
    if name == "get_memories":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        rows = await pool.fetch("SELECT pkid, title, description, created_at FROM memories WHERE agent_id = $1 ORDER BY pkid", agent["agent_id"])
        return {"agent": args["agent_name"], "memories": [dict(r) for r in rows]}

    if name == "get_memory":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        row = await pool.fetchrow("SELECT pkid, title, description, created_at FROM memories WHERE agent_id = $1 AND pkid = $2", agent["agent_id"], args["pkid"])
        return dict(row) if row else {"error": "Memory not found"}

    if name == "create_memory":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may write to its memories"}
        row = await pool.fetchrow("INSERT INTO memories (agent_id, title, description) VALUES ($1, $2, $3) RETURNING pkid, title, created_at", agent["agent_id"], args["title"], args.get("description"))
        return {"created": dict(row)}

    if name == "update_memory":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may modify its memories"}
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        values.extend([agent["agent_id"], args["pkid"]])
        row = await pool.fetchrow(f"UPDATE memories SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title", *values)
        return {"updated": dict(row)} if row else {"error": "Memory not found"}

    if name == "delete_memory":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may delete its memories"}
        result = await pool.execute("DELETE FROM memories WHERE agent_id = $1 AND pkid = $2", agent["agent_id"], args["pkid"])
        return {"deleted": args["pkid"]} if result != "DELETE 0" else {"error": "Memory not found"}

    # ── Preferences ───────────────────────────────────────────────
    if name == "get_preferences":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        rows = await pool.fetch("SELECT pkid, title FROM preferences WHERE agent_id = $1 AND parent_id = 0 ORDER BY pkid", agent["agent_id"])
        return {"agent": args["agent_name"], "preferences": [dict(r) for r in rows]}

    if name == "get_preference":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        node = await pool.fetchrow("SELECT pkid, parent_id, title, description FROM preferences WHERE agent_id = $1 AND pkid = $2", agent["agent_id"], args["pkid"])
        if not node:
            return {"error": "Preference not found"}
        children = await pool.fetch("SELECT pkid, parent_id, title, description FROM preferences WHERE agent_id = $1 AND parent_id = $2 ORDER BY pkid", agent["agent_id"], args["pkid"])
        return {"node": dict(node), "children": [dict(r) for r in children]}

    if name == "create_preference":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may write to its preferences"}
        row = await pool.fetchrow("INSERT INTO preferences (agent_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING pkid, title", agent["agent_id"], args.get("parent_id", 0), args["title"], args.get("description"))
        return {"created": dict(row)}

    if name == "update_preference":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may modify its preferences"}
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        updates.append("updated_at = NOW()")
        values.extend([agent["agent_id"], args["pkid"]])
        row = await pool.fetchrow(f"UPDATE preferences SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title", *values)
        return {"updated": dict(row)} if row else {"error": "Preference not found"}

    if name == "delete_preference":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may delete its preferences"}
        result = await pool.execute("WITH RECURSIVE subtree AS (SELECT pkid FROM preferences WHERE agent_id = $1 AND pkid = $2 UNION ALL SELECT p.pkid FROM preferences p INNER JOIN subtree s ON p.parent_id = s.pkid WHERE p.agent_id = $1) DELETE FROM preferences WHERE pkid IN (SELECT pkid FROM subtree)", agent["agent_id"], args["pkid"])
        count = int(result.split()[-1])
        return {"deleted": args["pkid"], "descendants_deleted": count - 1} if count > 0 else {"error": "Preference not found"}

    # ── Projects ──────────────────────────────────────────────────
    if name == "get_projects":
        rows = await pool.fetch("SELECT project_id, title, description FROM projects WHERE user_id = $1 ORDER BY project_id", caller["user_id"])
        projects = []
        for r in rows:
            p = dict(r)
            p["url"] = _browse_url(f"/projects/{p['project_id']}/document", agent_key)
            projects.append(p)
        return {"projects": projects}

    if name == "get_project":
        project = await pool.fetchrow("SELECT project_id, title, description FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not project:
            return {"error": "Project not found"}
        pid = args["project_id"]
        proj = dict(project)
        proj["url"] = _browse_url(f"/projects/{pid}/document", agent_key)
        sections = await pool.fetch("SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 ORDER BY parent_id, section_id", pid)
        secs = [dict(r) for r in sections]
        return {"project": proj, "sections": secs}

    if name == "get_section":
        project = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not project:
            return {"error": "Project not found"}
        pid = args["project_id"]
        node = await pool.fetchrow("SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 AND section_id = $2", pid, args["section_id"])
        if not node:
            return {"error": "Section not found"}
        sec = dict(node)
        children = await pool.fetch("SELECT section_id, parent_id, title, description, file_path FROM project_sections WHERE project_id = $1 AND parent_id = $2 ORDER BY section_id", pid, args["section_id"])
        return {"section": sec, "children": [dict(r) for r in children]}

    if name == "create_project":
        row = await pool.fetchrow("INSERT INTO projects (user_id, title, description) VALUES ($1, $2, $3) RETURNING project_id, title", caller["user_id"], args["title"], args.get("description"))
        return {"created": dict(row)}

    if name == "create_section":
        project = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not project:
            return {"error": "Project not found"}
        row = await pool.fetchrow("INSERT INTO project_sections (project_id, parent_id, title, description, file_path) VALUES ($1, $2, $3, $4, $5) RETURNING section_id, title", args["project_id"], args.get("parent_id", 0), args["title"], args.get("description"), args.get("file_path"))
        return {"created": dict(row)}

    if name == "update_project":
        existing = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not existing:
            return {"error": "Project not found"}
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        updates.append("updated_at = NOW()")
        values.append(args["project_id"])
        row = await pool.fetchrow(f"UPDATE projects SET {', '.join(updates)} WHERE project_id = ${idx} RETURNING project_id, title", *values)
        return {"updated": dict(row)}

    if name == "update_section":
        project = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not project:
            return {"error": "Project not found"}
        updates, values, idx = [], [], 1
        for field in ("title", "description", "file_path"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        updates.append("updated_at = NOW()")
        values.extend([args["project_id"], args["section_id"]])
        row = await pool.fetchrow(f"UPDATE project_sections SET {', '.join(updates)} WHERE project_id = ${idx} AND section_id = ${idx + 1} RETURNING section_id, title", *values)
        return {"updated": dict(row)} if row else {"error": "Section not found"}

    if name == "delete_project":
        project = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not project:
            return {"error": "Project not found"}
        sec_result = await pool.execute("DELETE FROM project_sections WHERE project_id = $1", args["project_id"])
        await pool.execute("DELETE FROM projects WHERE project_id = $1", args["project_id"])
        return {"deleted": args["project_id"], "sections_deleted": int(sec_result.split()[-1])}

    if name == "delete_section":
        project = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", args["project_id"], caller["user_id"])
        if not project:
            return {"error": "Project not found"}
        result = await pool.execute("WITH RECURSIVE subtree AS (SELECT section_id FROM project_sections WHERE project_id = $1 AND section_id = $2 UNION ALL SELECT ps.section_id FROM project_sections ps INNER JOIN subtree s ON ps.parent_id = s.section_id WHERE ps.project_id = $1) DELETE FROM project_sections WHERE section_id IN (SELECT section_id FROM subtree)", args["project_id"], args["section_id"])
        count = int(result.split()[-1])
        return {"deleted": args["section_id"], "descendants_deleted": count - 1} if count > 0 else {"error": "Section not found"}

    # ── Hints (user-scoped) ────────────────────────────────────────
    if name == "get_hints":
        rows = await pool.fetch("SELECT hint_id, parent_id, title, description FROM hints WHERE user_id = $1 ORDER BY parent_id, hint_id", caller["user_id"])
        return {"hints": [dict(r) for r in rows]}

    if name == "get_hint":
        node = await pool.fetchrow("SELECT hint_id, parent_id, title, description FROM hints WHERE user_id = $1 AND hint_id = $2", caller["user_id"], args["hint_id"])
        if not node:
            return {"error": "Hint not found"}
        children = await pool.fetch("SELECT hint_id, parent_id, title, description FROM hints WHERE user_id = $1 AND parent_id = $2 ORDER BY hint_id", caller["user_id"], args["hint_id"])
        return {"node": dict(node), "children": [dict(r) for r in children]}

    if name == "create_hint":
        row = await pool.fetchrow("INSERT INTO hints (user_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING hint_id, parent_id, title", caller["user_id"], args.get("parent_id", 0), args["title"], args.get("description"))
        return {"created": dict(row)}

    if name == "update_hint":
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        updates.append("updated_at = NOW()")
        values.extend([caller["user_id"], args["hint_id"]])
        row = await pool.fetchrow(f"UPDATE hints SET {', '.join(updates)} WHERE user_id = ${idx} AND hint_id = ${idx + 1} RETURNING hint_id, title", *values)
        return {"updated": dict(row)} if row else {"error": "Hint not found"}

    if name == "delete_hint":
        result = await pool.execute("WITH RECURSIVE subtree AS (SELECT hint_id FROM hints WHERE user_id = $1 AND hint_id = $2 UNION ALL SELECT h.hint_id FROM hints h INNER JOIN subtree s ON h.parent_id = s.hint_id WHERE h.user_id = $1) DELETE FROM hints WHERE hint_id IN (SELECT hint_id FROM subtree)", caller["user_id"], args["hint_id"])
        count = int(result.split()[-1])
        return {"deleted": args["hint_id"], "descendants_deleted": count - 1} if count > 0 else {"error": "Hint not found"}

    # ── Wikis (user-scoped) ─────────────────────────────────────────
    if name == "get_wikis":
        rows = await pool.fetch("SELECT wiki_id, title, description, updated_at FROM wikis WHERE user_id = $1 ORDER BY wiki_id", caller["user_id"])
        wikis = []
        for r in rows:
            w = dict(r)
            w["url"] = _browse_url(f"/wikis/{w['wiki_id']}/document", agent_key)
            wikis.append(w)
        return {"wikis": wikis}

    if name == "get_wiki":
        wiki = await pool.fetchrow("SELECT wiki_id, title, description, updated_at FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        wid = args["wiki_id"]
        wiki_dict = dict(wiki)
        wiki_dict["url"] = _browse_url(f"/wikis/{wid}/document", agent_key)
        sections = await pool.fetch("SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 ORDER BY parent_id, section_id", wid)
        section_ids = [r["section_id"] for r in sections]
        tags_by_section: dict[int, list[str]] = {sid: [] for sid in section_ids}
        if section_ids:
            tag_rows = await pool.fetch("SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag", section_ids)
            for tr in tag_rows:
                tags_by_section[tr["section_id"]].append(tr["tag"])
        section_list = []
        for r in sections:
            s = dict(r)
            s["tags"] = tags_by_section.get(r["section_id"], [])
            section_list.append(s)
        return {"wiki": wiki_dict, "sections": section_list}

    if name == "create_wiki":
        row = await pool.fetchrow("INSERT INTO wikis (user_id, title, description) VALUES ($1, $2, $3) RETURNING wiki_id, title", caller["user_id"], args["title"], args.get("description"))
        return {"created": dict(row)}

    if name == "update_wiki":
        existing = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not existing:
            return {"error": "Wiki not found"}
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        if not updates:
            return {"error": "No fields to update"}
        updates.append("updated_at = NOW()")
        values.append(args["wiki_id"])
        row = await pool.fetchrow(f"UPDATE wikis SET {', '.join(updates)} WHERE wiki_id = ${idx} RETURNING wiki_id, title", *values)
        return {"updated": dict(row)}

    if name == "delete_wiki":
        wiki = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        sec_result = await pool.execute("DELETE FROM wiki_sections WHERE wiki_id = $1", args["wiki_id"])
        await pool.execute("DELETE FROM wikis WHERE wiki_id = $1", args["wiki_id"])
        return {"deleted": args["wiki_id"], "sections_deleted": int(sec_result.split()[-1])}

    if name == "create_wiki_section":
        wiki = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        tags = args.get("tags") or []
        async with pool.acquire() as conn:
            async with conn.transaction():
                row = await conn.fetchrow("INSERT INTO wiki_sections (wiki_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING section_id, title", args["wiki_id"], args.get("parent_id", 0), args["title"], args.get("description"))
                if tags:
                    await conn.executemany("INSERT INTO wiki_section_tags (section_id, tag) VALUES ($1, $2)", [(row["section_id"], tag) for tag in tags])
                await conn.execute("UPDATE wikis SET updated_at = NOW() WHERE wiki_id = $1", args["wiki_id"])
        result = dict(row)
        result["tags"] = tags
        return {"created": result}

    if name == "get_wiki_section":
        wiki = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        wid = args["wiki_id"]
        node = await pool.fetchrow("SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 AND section_id = $2", wid, args["section_id"])
        if not node:
            return {"error": "Section not found"}
        children = await pool.fetch("SELECT section_id, parent_id, title, description, updated_at FROM wiki_sections WHERE wiki_id = $1 AND parent_id = $2 ORDER BY section_id", wid, args["section_id"])
        all_ids = [node["section_id"]] + [c["section_id"] for c in children]
        tag_rows = await pool.fetch("SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag", all_ids)
        tags_by_sec: dict[int, list[str]] = {sid: [] for sid in all_ids}
        for tr in tag_rows:
            tags_by_sec[tr["section_id"]].append(tr["tag"])
        section_dict = dict(node)
        section_dict["tags"] = tags_by_sec[node["section_id"]]
        children_list = []
        for c in children:
            cd = dict(c)
            cd["tags"] = tags_by_sec[c["section_id"]]
            children_list.append(cd)
        return {"section": section_dict, "children": children_list}

    if name == "update_wiki_section":
        wiki = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        updates, values, idx = [], [], 1
        for field in ("title", "description"):
            if field in args:
                updates.append(f"{field} = ${idx}"); values.append(args[field]); idx += 1
        has_field_updates = bool(updates)
        has_tag_updates = "tags" in args
        if not has_field_updates and not has_tag_updates:
            return {"error": "No fields to update"}
        async with pool.acquire() as conn:
            async with conn.transaction():
                if has_field_updates:
                    updates.append("updated_at = NOW()")
                    values.extend([args["wiki_id"], args["section_id"]])
                    row = await conn.fetchrow(f"UPDATE wiki_sections SET {', '.join(updates)} WHERE wiki_id = ${idx} AND section_id = ${idx + 1} RETURNING section_id, title", *values)
                    if not row:
                        return {"error": "Section not found"}
                else:
                    row = await conn.fetchrow("UPDATE wiki_sections SET updated_at = NOW() WHERE wiki_id = $1 AND section_id = $2 RETURNING section_id, title", args["wiki_id"], args["section_id"])
                    if not row:
                        return {"error": "Section not found"}
                if has_tag_updates:
                    await conn.execute("DELETE FROM wiki_section_tags WHERE section_id = $1", args["section_id"])
                    tags = args.get("tags") or []
                    if tags:
                        await conn.executemany("INSERT INTO wiki_section_tags (section_id, tag) VALUES ($1, $2)", [(args["section_id"], tag) for tag in tags])
                await conn.execute("UPDATE wikis SET updated_at = NOW() WHERE wiki_id = $1", args["wiki_id"])
        result = dict(row)
        if has_tag_updates:
            result["tags"] = args.get("tags") or []
        return {"updated": result}

    if name == "delete_wiki_section":
        wiki = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        async with pool.acquire() as conn:
            async with conn.transaction():
                result = await conn.execute("WITH RECURSIVE subtree AS (SELECT section_id FROM wiki_sections WHERE wiki_id = $1 AND section_id = $2 UNION ALL SELECT ws.section_id FROM wiki_sections ws INNER JOIN subtree s ON ws.parent_id = s.section_id WHERE ws.wiki_id = $1) DELETE FROM wiki_sections WHERE section_id IN (SELECT section_id FROM subtree)", args["wiki_id"], args["section_id"])
                deleted_count = int(result.split()[-1])
                if deleted_count == 0:
                    return {"error": "Section not found"}
                await conn.execute("UPDATE wikis SET updated_at = NOW() WHERE wiki_id = $1", args["wiki_id"])
        return {"deleted": args["section_id"], "descendants_deleted": deleted_count - 1}

    if name == "get_wiki_tags":
        wiki = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", args["wiki_id"], caller["user_id"])
        if not wiki:
            return {"error": "Wiki not found"}
        rows = await pool.fetch("SELECT DISTINCT wst.tag FROM wiki_section_tags wst JOIN wiki_sections ws ON wst.section_id = ws.section_id WHERE ws.wiki_id = $1 ORDER BY wst.tag", args["wiki_id"])
        return {"wiki_id": args["wiki_id"], "tags": [r["tag"] for r in rows]}

    if name == "search_wiki_tag":
        rows = await pool.fetch("""
            SELECT w.wiki_id, w.title AS wiki_title,
                   ws.section_id, ws.title, ws.description, ws.updated_at
            FROM wiki_section_tags wst
            JOIN wiki_sections ws ON wst.section_id = ws.section_id
            JOIN wikis w ON ws.wiki_id = w.wiki_id
            WHERE wst.tag = $1 AND w.user_id = $2
            ORDER BY w.wiki_id, ws.section_id
        """, args["tag"], caller["user_id"])
        section_ids = [r["section_id"] for r in rows]
        tags_by_section_search: dict[int, list[str]] = {sid: [] for sid in section_ids}
        if section_ids:
            tag_rows = await pool.fetch("SELECT section_id, tag FROM wiki_section_tags WHERE section_id = ANY($1) ORDER BY section_id, tag", section_ids)
            for tr in tag_rows:
                tags_by_section_search[tr["section_id"]].append(tr["tag"])
        sections = []
        for r in rows:
            sections.append({"wiki_id": r["wiki_id"], "wiki_title": r["wiki_title"], "section_id": r["section_id"], "title": r["title"], "description": r["description"], "updated_at": r["updated_at"], "tags": tags_by_section_search[r["section_id"]]})
        return {"tag": args["tag"], "sections": sections}

    # ── Sharing ───────────────────────────────────────────────────
    if name == "share_object":
        if args["object_type_id"] not in (1, 2, 3):
            return {"error": "object_type_id must be 1 (project), 2 (hint), or 3 (wiki)"}
        perm = args.get("permission_level", 1)
        if perm not in (1, 2, 3):
            return {"error": "permission_level must be 1, 2, or 3"}
        if args["shared_to_user_id"] == caller["user_id"]:
            return {"error": "Cannot share with yourself"}
        target = await pool.fetchrow("SELECT user_id FROM users WHERE user_id = $1", args["shared_to_user_id"])
        if not target:
            return {"error": "Target user not found"}
        otype = args["object_type_id"]
        oid = args["object_id"]
        if otype == 1:
            owner_check = await pool.fetchrow("SELECT project_id FROM projects WHERE project_id = $1 AND user_id = $2", oid, caller["user_id"])
        elif otype == 2:
            owner_check = await pool.fetchrow("SELECT hint_id FROM hints WHERE hint_id = $1 AND user_id = $2 AND parent_id = 0", oid, caller["user_id"])
        elif otype == 3:
            owner_check = await pool.fetchrow("SELECT wiki_id FROM wikis WHERE wiki_id = $1 AND user_id = $2", oid, caller["user_id"])
        if not owner_check:
            return {"error": "Object not found or you don't own it"}
        row = await pool.fetchrow(
            """INSERT INTO shared_objects (shared_by_user_id, shared_to_user_id, object_type_id, object_id, permission_level)
               VALUES ($1, $2, $3, $4, $5)
               ON CONFLICT (shared_to_user_id, object_type_id, object_id) DO UPDATE SET permission_level = $5
               RETURNING share_id, object_type_id, object_id, shared_to_user_id, permission_level""",
            caller["user_id"], args["shared_to_user_id"], otype, oid, perm
        )
        return {"shared": dict(row)}

    if name == "revoke_share":
        row = await pool.fetchrow(
            "DELETE FROM shared_objects WHERE share_id = $1 AND shared_by_user_id = $2 RETURNING share_id",
            args["share_id"], caller["user_id"]
        )
        if not row:
            return {"error": "Share not found or you didn't create it"}
        return {"revoked": row["share_id"]}

    if name == "get_shared_by_me":
        rows = await pool.fetch(
            """SELECT so.share_id, so.object_type_id, ot.name as object_type, so.object_id,
                      so.shared_to_user_id, u.name as shared_to_name, so.permission_level
               FROM shared_objects so
               JOIN object_types ot ON so.object_type_id = ot.object_type_id
               JOIN users u ON so.shared_to_user_id = u.user_id
               WHERE so.shared_by_user_id = $1
               ORDER BY so.object_type_id, so.object_id""",
            caller["user_id"]
        )
        return {"shared_by_me": [dict(r) for r in rows]}

    if name == "get_shared_to_me":
        rows = await pool.fetch(
            """SELECT so.share_id, so.object_type_id, ot.name as object_type, so.object_id,
                      so.shared_by_user_id, u.name as shared_by_name, so.permission_level
               FROM shared_objects so
               JOIN object_types ot ON so.object_type_id = ot.object_type_id
               JOIN users u ON so.shared_by_user_id = u.user_id
               WHERE so.shared_to_user_id = $1
               ORDER BY so.object_type_id, so.object_id""",
            caller["user_id"]
        )
        return {"shared_to_me": [dict(r) for r in rows]}

    # ── Sessions ──────────────────────────────────────────────────
    if name == "create_session":
        row = await pool.fetchrow("INSERT INTO sessions (agent_id, project) VALUES ($1, $2) RETURNING session_id, started_at", caller["agent_id"], args.get("project"))
        return {"session_id": row["session_id"], "agent": caller["agent_name"], "started_at": row["started_at"].isoformat(), "project": args.get("project")}

    if name == "get_last_session":
        row = await pool.fetchrow("SELECT session_id, started_at, project FROM sessions WHERE agent_id = $1 ORDER BY started_at DESC LIMIT 1", caller["agent_id"])
        if not row:
            return {"last_session": None}
        return {"agent": caller["agent_name"], "last_session": {"session_id": row["session_id"], "started_at": row["started_at"].isoformat(), "project": row["project"]}}

    # ── Save ──────────────────────────────────────────────────────
    if name == "save_notes":
        import smtplib
        import threading
        from email.mime.multipart import MIMEMultipart
        from email.mime.text import MIMEText
        from email.mime.base import MIMEBase
        from email import encoders

        subject, content = args["subject"], args["content"]
        timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
        filename = f"lucy-notes-{timestamp}.md"
        smtp_pass = os.environ.get("LUCYAPI_SMTP_PASS", "")

        def _send():
            msg = MIMEMultipart()
            msg["From"] = "rick@snowcapsystems.com"
            msg["To"] = "rick@snowcapsystems.com"
            msg["Subject"] = subject
            msg.attach(MIMEText(f"Lucy saved content.\n\nSubject: {subject}\nTimestamp: {timestamp} UTC", "plain"))
            att = MIMEBase("application", "octet-stream")
            att.set_payload(content.encode("utf-8"))
            encoders.encode_base64(att)
            att.add_header("Content-Disposition", f'attachment; filename="{filename}"')
            msg.attach(att)
            try:
                with smtplib.SMTP_SSL("smtp.forwardemail.net", 465) as srv:
                    srv.login("rick@snowcapsystems.com", smtp_pass)
                    srv.sendmail("rick@snowcapsystems.com", "rick@snowcapsystems.com", msg.as_string())
            except Exception:
                pass

        threading.Thread(target=_send, daemon=True).start()
        return {"status": "accepted", "filename": filename, "to": "rick@snowcapsystems.com"}

    # ── Secrets (user-scoped) ────────────────────────────────────
    if name == "list_secrets":
        rows = await pool.fetch("SELECT key, created_at, updated_at FROM secrets WHERE user_id = $1 ORDER BY key", caller["user_id"])
        return {"secrets": [dict(r) for r in rows]}

    if name == "get_secret":
        row = await pool.fetchrow("SELECT key, encrypted_value FROM secrets WHERE user_id = $1 AND key = $2", caller["user_id"], args["key"])
        if not row:
            return {"error": "Secret not found"}
        return {"key": row["key"], "value": decrypt(row["encrypted_value"])}

    if name == "set_secret":
        encrypted = encrypt(args["value"])
        row = await pool.fetchrow(
            "INSERT INTO secrets (user_id, key, encrypted_value) VALUES ($1, $2, $3) "
            "ON CONFLICT ON CONSTRAINT uq_secrets_user_key "
            "DO UPDATE SET encrypted_value = EXCLUDED.encrypted_value, updated_at = NOW() "
            "RETURNING secret_id, key, created_at, updated_at",
            caller["user_id"], args["key"], encrypted
        )
        return {"saved": dict(row)}

    if name == "delete_secret":
        result = await pool.execute("DELETE FROM secrets WHERE user_id = $1 AND key = $2", caller["user_id"], args["key"])
        return {"deleted": args["key"]} if result != "DELETE 0" else {"error": "Secret not found"}

    # ── Handoffs (agent-scoped, cross-agent read/create) ──────────
    if name == "list_handoffs":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        rows = await pool.fetch("SELECT handoff_id, title, prompt, created_at FROM handoffs WHERE agent_id = $1 AND picked_up_at IS NULL ORDER BY created_at", agent["agent_id"])
        return {"agent": args["agent_name"], "handoffs": [dict(r) for r in rows]}

    if name == "get_handoff":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        row = await pool.fetchrow("SELECT handoff_id, title, prompt, created_at, picked_up_at FROM handoffs WHERE agent_id = $1 AND handoff_id = $2", agent["agent_id"], args["handoff_id"])
        return dict(row) if row else {"error": "Handoff not found"}

    if name == "create_handoff":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        row = await pool.fetchrow("INSERT INTO handoffs (agent_id, title, prompt) VALUES ($1, $2, $3) RETURNING handoff_id, title, created_at", agent["agent_id"], args["title"], args["prompt"])
        return {"created": dict(row)}

    if name == "pickup_handoff":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may pickup its handoffs"}
        row = await pool.fetchrow("UPDATE handoffs SET picked_up_at = NOW() WHERE agent_id = $1 AND handoff_id = $2 AND picked_up_at IS NULL RETURNING handoff_id, title, picked_up_at", agent["agent_id"], args["handoff_id"])
        return {"picked_up": dict(row)} if row else {"error": "Handoff not found or already picked up"}

    if name == "delete_handoff":
        agent = await _get_agent(args["agent_name"], caller)
        if not agent:
            return {"error": "Agent not found or access denied"}
        if caller["agent_name"] != args["agent_name"]:
            return {"error": "Only the named agent may delete its handoffs"}
        result = await pool.execute("DELETE FROM handoffs WHERE agent_id = $1 AND handoff_id = $2", agent["agent_id"], args["handoff_id"])
        return {"deleted": args["handoff_id"]} if result != "DELETE 0" else {"error": "Handoff not found"}

    # ── Images (Gemini) ────────────────────────────────────────────
    if name == "generate_image":
        import httpx
        body = {"prompt": args["prompt"], "model": args.get("model", "nano-banana"), "aspect_ratio": args.get("aspect_ratio", "1:1")}
        async with httpx.AsyncClient(timeout=120) as client:
            resp = await client.post(f"{BASE_URL}/genimage", json=body)
            resp.raise_for_status()
            return resp.json()

    if name == "edit_image":
        import httpx
        body = {"prompt": args["prompt"], "model": args.get("model", "nano-banana")}
        if "image_id" in args:
            body["image_id"] = args["image_id"]
        if "image_url" in args:
            body["image_url"] = args["image_url"]
        async with httpx.AsyncClient(timeout=120) as client:
            resp = await client.post(f"{BASE_URL}/genimage/edit", json=body)
            resp.raise_for_status()
            return resp.json()

    if name == "analyze_image":
        import httpx
        body = {"prompt": args.get("prompt", "Describe this image in detail")}
        if "image_id" in args:
            body["image_id"] = args["image_id"]
        if "image_url" in args:
            body["image_url"] = args["image_url"]
        async with httpx.AsyncClient(timeout=60) as client:
            resp = await client.post(f"{BASE_URL}/genimage/analyze", json=body)
            resp.raise_for_status()
            return resp.json()

    if name == "list_images":
        params = {}
        if "keep" in args:
            params["keep"] = str(args["keep"]).lower()
        if "limit" in args:
            params["limit"] = args["limit"]
        import httpx
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.get(f"{BASE_URL}/images", params=params)
            resp.raise_for_status()
            return resp.json()

    if name == "keep_image":
        import httpx
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.patch(f"{BASE_URL}/images/{args['image_id']}", json={"keep": True})
            resp.raise_for_status()
            return resp.json()

    if name == "delete_image":
        import httpx
        params = {}
        if args.get("force"):
            params["force"] = "true"
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.delete(f"{BASE_URL}/images/{args['image_id']}", params=params)
            resp.raise_for_status()
            return resp.json()

    if name == "cleanup_images":
        import httpx
        async with httpx.AsyncClient(timeout=60) as client:
            resp = await client.post(f"{BASE_URL}/images/cleanup")
            resp.raise_for_status()
            return resp.json()

    # ── Google Docs ─────────────────────────────────────────────────
    if name == "create_google_doc":
        content_blocks = args.get("content")
        if content_blocks:
            return await google_client.create_formatted_document(
                caller["user_id"], args["title"], content_blocks, args.get("branding", "none")
            )
        return await google_client.create_document(
            caller["user_id"], args["title"], args.get("body", "")
        )

    if name == "read_google_doc":
        return await google_client.read_document(
            caller["user_id"], args["doc_id"]
        )

    if name == "update_google_doc":
        return await google_client.update_document(
            caller["user_id"], args["doc_id"], args["content"], args.get("branding", "none")
        )

    if name == "append_google_doc":
        return await google_client.append_to_document(
            caller["user_id"], args["doc_id"], args["content"], args.get("branding", "none")
        )

    # ── Google Drive file management ────────────────────────────────
    if name == "list_google_files":
        return await google_client.list_files(
            caller["user_id"], args.get("folder_id")
        )

    if name == "create_google_folder":
        return await google_client.create_folder(
            caller["user_id"], args["name"], args.get("parent_folder_id")
        )

    if name == "move_google_file":
        return await google_client.move_file(
            caller["user_id"], args["file_id"], args["target_folder_id"]
        )

    if name == "delete_google_file":
        return await google_client.delete_file(
            caller["user_id"], args["file_id"]
        )

    if name == "get_google_file_meta":
        return await google_client.get_file_metadata(
            caller["user_id"], args["file_id"]
        )

    return {"error": f"Unknown tool: {name}"}


def create_mcp_session_manager() -> StreamableHTTPSessionManager:
    """Create a stateless MCP session manager."""
    return StreamableHTTPSessionManager(
        app=create_mcp_server(),
        event_store=None,
        json_response=False,
        stateless=True,
    )
