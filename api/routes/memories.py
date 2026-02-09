from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


class MemoryCreate(BaseModel):
    title: str
    description: Optional[str] = None


class MemoryUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None


@router.get("/agents/{agent_name}/memories")
async def get_memories(agent_name: str, caller: dict = Depends(verify_api_key)):
    """All memories with full descriptions."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        raise HTTPException(status_code=404, detail="Not found or access denied")

    rows = await pool.fetch(
        "SELECT pkid, title, description, created_at FROM memories WHERE agent_id = $1 ORDER BY pkid",
        agent["agent_id"]
    )
    return {"agent": agent_name, "memories": [dict(r) for r in rows]}


@router.get("/agents/{agent_name}/memories/{pkid}")
async def get_memory(agent_name: str, pkid: int, caller: dict = Depends(verify_api_key)):
    """Single memory with full description."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        raise HTTPException(status_code=404, detail="Not found or access denied")

    row = await pool.fetchrow(
        "SELECT pkid, title, description, created_at FROM memories WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not row:
        raise HTTPException(status_code=404, detail="Memory not found")
    return dict(row)


@router.post("/agents/{agent_name}/memories")
async def create_memory(agent_name: str, memory: MemoryCreate, caller: dict = Depends(verify_api_key)):
    """Create a new memory. Agents may call this freely (premise 8)."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id, name FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")

    # Only the named agent can write to its own memories
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may write to its memories")

    row = await pool.fetchrow(
        "INSERT INTO memories (agent_id, title, description) VALUES ($1, $2, $3) RETURNING pkid, title, created_at",
        agent["agent_id"], memory.title, memory.description
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/memories/{pkid}")
async def update_memory(agent_name: str, pkid: int, memory: MemoryUpdate, caller: dict = Depends(verify_api_key)):
    """Update a memory. Requires user approval (enforced by agent behavior, not API)."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may modify its memories")

    existing = await pool.fetchrow(
        "SELECT pkid FROM memories WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Memory not found")

    updates = []
    values = []
    idx = 1
    if memory.title is not None:
        updates.append(f"title = ${idx}")
        values.append(memory.title)
        idx += 1
    if memory.description is not None:
        updates.append(f"description = ${idx}")
        values.append(memory.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    values.append(agent["agent_id"])
    values.append(pkid)
    sql = f"UPDATE memories SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title"
    row = await pool.fetchrow(sql, *values)
    return {"updated": dict(row)}


@router.delete("/agents/{agent_name}/memories/{pkid}")
async def delete_memory(agent_name: str, pkid: int, caller: dict = Depends(verify_api_key)):
    """Delete a memory. Requires user approval (enforced by agent behavior, not API)."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may delete its memories")

    result = await pool.execute(
        "DELETE FROM memories WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Memory not found")
    return {"deleted": pkid}
