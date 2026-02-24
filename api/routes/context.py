import os
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..auth import verify_api_key
from ..database import get_pool

SAVE_TOKEN = os.environ.get("LUCYAPI_SAVE_TOKEN", "")

router = APIRouter()


@router.get("/boot")
async def boot(agent_key: str = None, caller: dict = Depends(verify_api_key)):
    """Bootstrap endpoint for mobile/web agents. Returns always_load context plus fully qualified endpoint URLs with agent_key baked in."""
    result = await get_always_load(caller["agent_name"], caller)
    
    base = "https://lucyapi.snowcapsystems.com"
    key = f"agent_key={agent_key}" if agent_key else ""
    name = caller["agent_name"]
    
    def url(path, has_params=False):
        sep = "&" if has_params else "?"
        return f"{base}{path}{sep}{key}" if key else f"{base}{path}"
    
    endpoints = {
        "time": f"{base}/time",
        "context": url(f"/agents/{name}/context"),
        "always_load": url(f"/agents/{name}/context/always_load"),
        "always_load_item": url(f"/agents/{name}/context/always_load/{{pkid}}"),
        "memories": url(f"/agents/{name}/memories"),
        "memory_item": url(f"/agents/{name}/memories/{{pkid}}"),
        "preferences": url(f"/agents/{name}/preferences"),
        "preference_item": url(f"/agents/{name}/preferences/{{pkid}}"),
        "projects": url("/projects"),
        "project_item": url("/projects/{project_id}"),
        "project_section": url("/projects/{project_id}/sections/{section_id}"),
        "project_document": url("/projects/{project_id}/document"),
        "session_start": url("/sessions"),
        "session_last": url("/sessions/last"),
        "save": {
            "url": f"{base}/save/{SAVE_TOKEN}",
            "usage": "POST JSON {subject, content} or GET with ?subject=...&content=... (URL-encoded). Emails markdown attachment to Rick."
        },
    }
    
    return {"endpoints": endpoints, **result}


@router.get("/agents/{agent_name}/context")
async def get_agent_context(agent_name: str, caller: dict = Depends(verify_api_key)):
    """Full context payload: always-load titles, memory titles, preferences manifest, project manifest."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        return {"error": "Agent not found"}, 404

    # Verify caller belongs to same user (read access)
    if agent["user_id"] != caller["user_id"]:
        return {"error": "Access denied"}, 403

    # Always-load: full tree (titles + descriptions for root nodes, titles only for children)
    always_load_rows = await pool.fetch(
        "SELECT pkid, parent_id, title FROM always_load WHERE agent_id = $1 ORDER BY parent_id, pkid",
        agent["agent_id"]
    )

    # Memory titles (always-load for ambient recall)
    memory_rows = await pool.fetch(
        "SELECT pkid, title FROM memories WHERE agent_id = $1 ORDER BY pkid",
        agent["agent_id"]
    )

    # Preferences: top-level titles only (manifest for on-demand loading)
    pref_rows = await pool.fetch(
        "SELECT pkid, title FROM preferences WHERE agent_id = $1 AND parent_id = 0 ORDER BY pkid",
        agent["agent_id"]
    )

    # Projects: titles + status (manifest for on-demand loading)
    project_rows = await pool.fetch(
        """SELECT p.project_id, p.title, ps.code as status
           FROM projects p JOIN project_statuses ps ON p.status_id = ps.status_id
           WHERE p.user_id = $1 ORDER BY p.project_id""",
        agent["user_id"]
    )

    return {
        "agent": agent_name,
        "always_load": [dict(r) for r in always_load_rows],
        "memories": [dict(r) for r in memory_rows],
        "preferences_manifest": [dict(r) for r in pref_rows],
        "projects_manifest": [dict(r) for r in project_rows]
    }


@router.get("/agents/{agent_name}/context/always_load")
async def get_always_load(agent_name: str, caller: dict = Depends(verify_api_key)):
    """Full always-load tree with descriptions."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        return {"error": "Not found or access denied"}, 404

    rows = await pool.fetch(
        "SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 ORDER BY parent_id, pkid",
        agent["agent_id"]
    )
    return {"agent": agent_name, "always_load": [dict(r) for r in rows]}


@router.get("/agents/{agent_name}/context/always_load/{pkid}")
async def get_always_load_item(agent_name: str, pkid: int, caller: dict = Depends(verify_api_key)):
    """Single always-load node with its children."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        return {"error": "Not found or access denied"}, 404

    node = await pool.fetchrow(
        "SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    children = await pool.fetch(
        "SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 AND parent_id = $2 ORDER BY pkid",
        agent["agent_id"], pkid
    )
    return {
        "node": dict(node) if node else None,
        "children": [dict(r) for r in children]
    }


class AlwaysLoadCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None


class AlwaysLoadUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None


@router.post("/agents/{agent_name}/context/always_load")
async def create_always_load(agent_name: str, item: AlwaysLoadCreate, caller: dict = Depends(verify_api_key)):
    """Create an always_load node. Agent-scoped write."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may write to its always_load")

    row = await pool.fetchrow(
        "INSERT INTO always_load (agent_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING pkid, parent_id, title",
        agent["agent_id"], item.parent_id, item.title, item.description
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/context/always_load/{pkid}")
async def update_always_load(agent_name: str, pkid: int, item: AlwaysLoadUpdate, caller: dict = Depends(verify_api_key)):
    """Update an always_load node. Agent-scoped write."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may modify its always_load")

    existing = await pool.fetchrow(
        "SELECT pkid FROM always_load WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Node not found")

    updates = []
    values = []
    idx = 1
    if item.title is not None:
        updates.append(f"title = ${idx}")
        values.append(item.title)
        idx += 1
    if item.description is not None:
        updates.append(f"description = ${idx}")
        values.append(item.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.append(agent["agent_id"])
    values.append(pkid)
    sql = f"UPDATE always_load SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title"
    row = await pool.fetchrow(sql, *values)
    return {"updated": dict(row)}


@router.delete("/agents/{agent_name}/context/always_load/{pkid}")
async def delete_always_load(agent_name: str, pkid: int, caller: dict = Depends(verify_api_key)):
    """Delete an always_load node and its children. Agent-scoped write."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may delete its always_load")

    # Recursive delete: collect entire subtree then delete in one shot
    result = await pool.execute(
        """
        WITH RECURSIVE subtree AS (
            SELECT pkid FROM always_load WHERE agent_id = $1 AND pkid = $2
            UNION ALL
            SELECT a.pkid FROM always_load a
            INNER JOIN subtree s ON a.parent_id = s.pkid
            WHERE a.agent_id = $1
        )
        DELETE FROM always_load WHERE pkid IN (SELECT pkid FROM subtree)
        """,
        agent["agent_id"], pkid
    )
    deleted_count = int(result.split()[-1])
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Node not found")

    return {"deleted": pkid, "descendants_deleted": deleted_count - 1}
