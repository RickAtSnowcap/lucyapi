from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


class SessionCreate(BaseModel):
    project: Optional[str] = None


@router.post("/sessions")
async def create_session(session: SessionCreate, caller: dict = Depends(verify_api_key)):
    """Log a session start. Agent inferred from API key."""
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO sessions (agent_id, project) VALUES ($1, $2) RETURNING session_id, started_at",
        caller["agent_id"], session.project
    )
    return {
        "session_id": row["session_id"],
        "agent": caller["agent_name"],
        "started_at": row["started_at"].isoformat(),
        "project": session.project
    }


@router.get("/sessions/last")
async def get_last_session(caller: dict = Depends(verify_api_key)):
    """Get the most recent session for the calling agent."""
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT session_id, started_at, project FROM sessions WHERE agent_id = $1 ORDER BY started_at DESC LIMIT 1",
        caller["agent_id"]
    )
    if not row:
        return {"last_session": None}
    return {
        "agent": caller["agent_name"],
        "last_session": {
            "session_id": row["session_id"],
            "started_at": row["started_at"].isoformat(),
            "project": row["project"]
        }
    }
