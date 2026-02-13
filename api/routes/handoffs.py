from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


class HandoffCreate(BaseModel):
    title: str
    prompt: str


async def _get_agent_for_user(agent_name: str, caller: dict) -> dict:
    """Look up agent by name, verify same-user access."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, user_id FROM agents WHERE name = $1", agent_name
    )
    if not agent or agent["user_id"] != caller["user_id"]:
        raise HTTPException(status_code=404, detail="Agent not found or access denied")
    return dict(agent)


@router.get("/agents/{agent_name}/handoffs")
async def list_handoffs(agent_name: str, caller: dict = Depends(verify_api_key)):
    """List pending handoffs (where picked_up_at IS NULL). Any user agent may read."""
    agent = await _get_agent_for_user(agent_name, caller)
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT handoff_id, title, prompt, created_at FROM handoffs WHERE agent_id = $1 AND picked_up_at IS NULL ORDER BY created_at",
        agent["agent_id"]
    )
    return {"agent": agent_name, "handoffs": [dict(r) for r in rows]}


@router.get("/agents/{agent_name}/handoffs/{handoff_id}")
async def get_handoff(agent_name: str, handoff_id: int, caller: dict = Depends(verify_api_key)):
    """Get a specific handoff. Any user agent may read."""
    agent = await _get_agent_for_user(agent_name, caller)
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT handoff_id, title, prompt, created_at, picked_up_at FROM handoffs WHERE agent_id = $1 AND handoff_id = $2",
        agent["agent_id"], handoff_id
    )
    if not row:
        raise HTTPException(status_code=404, detail="Handoff not found")
    return dict(row)


@router.post("/agents/{agent_name}/handoffs")
async def create_handoff(agent_name: str, body: HandoffCreate, caller: dict = Depends(verify_api_key)):
    """Create a handoff prompt. Any user agent may create (cross-agent delegation)."""
    agent = await _get_agent_for_user(agent_name, caller)
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO handoffs (agent_id, title, prompt) VALUES ($1, $2, $3) RETURNING handoff_id, title, created_at",
        agent["agent_id"], body.title, body.prompt
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/handoffs/{handoff_id}/pickup")
async def pickup_handoff(agent_name: str, handoff_id: int, caller: dict = Depends(verify_api_key)):
    """Mark a handoff as picked up. Only the named agent may pickup its own handoffs."""
    agent = await _get_agent_for_user(agent_name, caller)
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may pickup its handoffs")

    pool = await get_pool()
    row = await pool.fetchrow(
        "UPDATE handoffs SET picked_up_at = NOW() WHERE agent_id = $1 AND handoff_id = $2 AND picked_up_at IS NULL RETURNING handoff_id, title, picked_up_at",
        agent["agent_id"], handoff_id
    )
    if not row:
        raise HTTPException(status_code=404, detail="Handoff not found or already picked up")
    return {"picked_up": dict(row)}


@router.delete("/agents/{agent_name}/handoffs/{handoff_id}")
async def delete_handoff(agent_name: str, handoff_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a handoff. Only the named agent may delete its own handoffs."""
    agent = await _get_agent_for_user(agent_name, caller)
    if caller["agent_name"] != agent_name:
        raise HTTPException(status_code=403, detail="Only the named agent may delete its handoffs")

    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM handoffs WHERE agent_id = $1 AND handoff_id = $2",
        agent["agent_id"], handoff_id
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Handoff not found")
    return {"deleted": handoff_id}
