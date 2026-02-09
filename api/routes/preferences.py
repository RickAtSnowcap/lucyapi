from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


class PreferenceCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None


class PreferenceUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None


@router.get("/agents/{agent_name}/preferences")
async def get_preferences_tree(agent_name: str, caller: dict = Depends(verify_api_key)):
    """Top-level preference categories (manifest)."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        raise HTTPException(status_code=404, detail="Not found or access denied")

    rows = await pool.fetch(
        "SELECT pkid, title FROM preferences WHERE agent_id = $1 AND parent_id = 0 ORDER BY pkid",
        agent["agent_id"]
    )
    return {"agent": agent_name, "preferences": [dict(r) for r in rows]}


@router.get("/agents/{agent_name}/preferences/{pkid}")
async def get_preference_branch(agent_name: str, pkid: int, caller: dict = Depends(verify_api_key)):
    """A preference node and its immediate children."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        raise HTTPException(status_code=404, detail="Not found or access denied")

    node = await pool.fetchrow(
        "SELECT pkid, parent_id, title, description FROM preferences WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not node:
        raise HTTPException(status_code=404, detail="Preference not found")

    children = await pool.fetch(
        "SELECT pkid, parent_id, title, description FROM preferences WHERE agent_id = $1 AND parent_id = $2 ORDER BY pkid",
        agent["agent_id"], pkid
    )
    return {
        "node": dict(node),
        "children": [dict(r) for r in children]
    }


@router.post("/agents/{agent_name}/preferences")
async def create_preference(agent_name: str, pref: PreferenceCreate, caller: dict = Depends(verify_api_key)):
    """Create a preference node. User approval enforced by agent behavior."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may write to its preferences")

    row = await pool.fetchrow(
        "INSERT INTO preferences (agent_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING pkid, title",
        agent["agent_id"], pref.parent_id, pref.title, pref.description
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/preferences/{pkid}")
async def update_preference(agent_name: str, pkid: int, pref: PreferenceUpdate, caller: dict = Depends(verify_api_key)):
    """Update a preference. User approval enforced by agent behavior."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may modify its preferences")

    updates = []
    values = []
    idx = 1
    if pref.title is not None:
        updates.append(f"title = ${idx}")
        values.append(pref.title)
        idx += 1
    if pref.description is not None:
        updates.append(f"description = ${idx}")
        values.append(pref.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append(f"updated_at = NOW()")
    values.append(agent["agent_id"])
    values.append(pkid)
    sql = f"UPDATE preferences SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title"
    row = await pool.fetchrow(sql, *values)
    if not row:
        raise HTTPException(status_code=404, detail="Preference not found")
    return {"updated": dict(row)}


@router.delete("/agents/{agent_name}/preferences/{pkid}")
async def delete_preference(agent_name: str, pkid: int, caller: dict = Depends(verify_api_key)):
    """Delete a preference. User approval enforced by agent behavior."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may delete its preferences")

    # Recursive delete: collect entire subtree then delete in one shot
    result = await pool.execute(
        """
        WITH RECURSIVE subtree AS (
            SELECT pkid FROM preferences WHERE agent_id = $1 AND pkid = $2
            UNION ALL
            SELECT p.pkid FROM preferences p
            INNER JOIN subtree s ON p.parent_id = s.pkid
            WHERE p.agent_id = $1
        )
        DELETE FROM preferences WHERE pkid IN (SELECT pkid FROM subtree)
        """,
        agent["agent_id"], pkid
    )
    deleted_count = int(result.split()[-1])
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Preference not found")

    return {"deleted": pkid, "descendants_deleted": deleted_count - 1}
