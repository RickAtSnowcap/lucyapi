from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..user_auth import verify_user_token
from ..database import get_pool

router = APIRouter(prefix="/admin", tags=["admin"])


# ── Helpers ──────────────────────────────────────────────────────

async def _require_user_agent(agent_name: str, user: dict) -> dict:
    """Look up agent by name, verify it belongs to the JWT user."""
    pool = await get_pool()
    agent = await pool.fetchrow(
        "SELECT agent_id, name, user_id FROM agents WHERE name = $1 AND user_id = $2",
        agent_name, user["user_id"]
    )
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")
    return dict(agent)


def _build_tree(rows):
    """Build nested tree from flat rows with parent_id."""
    nodes = {r["pkid"]: {**dict(r), "children": []} for r in rows}
    roots = []
    for r in rows:
        node = nodes[r["pkid"]]
        if r["parent_id"] == 0:
            roots.append(node)
        elif r["parent_id"] in nodes:
            nodes[r["parent_id"]]["children"].append(node)
    return roots


# ── Pydantic models ─────────────────────────────────────────────

class AlwaysLoadCreate(BaseModel):
    title: str
    description: Optional[str] = None
    parent_id: int = 0

class AlwaysLoadUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None

class MemoryCreate(BaseModel):
    title: str
    description: Optional[str] = None

class MemoryUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None

class PreferenceCreate(BaseModel):
    title: str
    description: Optional[str] = None
    parent_id: int = 0

class PreferenceUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None

class HandoffCreate(BaseModel):
    title: str
    prompt: str


# ── Agents list ──────────────────────────────────────────────────

@router.get("/agents")
async def list_agents(user: dict = Depends(verify_user_token)):
    pool = await get_pool()
    rows = await pool.fetch(
        """
        SELECT a.agent_id, a.name,
               s.session_id, s.started_at, s.project
        FROM agents a
        LEFT JOIN LATERAL (
            SELECT session_id, started_at, project
            FROM sessions
            WHERE agent_id = a.agent_id
            ORDER BY started_at DESC
            LIMIT 1
        ) s ON true
        WHERE a.user_id = $1
        ORDER BY a.name
        """,
        user["user_id"]
    )
    agents = []
    for r in rows:
        agent = {"agent_id": r["agent_id"], "name": r["name"]}
        if r["session_id"]:
            agent["last_session"] = {
                "session_id": r["session_id"],
                "started_at": r["started_at"].isoformat(),
                "project": r["project"],
            }
        else:
            agent["last_session"] = None
        agents.append(agent)
    return {"agents": agents}


# ── Always Load ──────────────────────────────────────────────────

@router.get("/agents/{agent_name}/always-load")
async def get_always_load(agent_name: str, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 ORDER BY parent_id, pkid",
        agent["agent_id"]
    )
    return {"agent": agent_name, "tree": _build_tree(rows)}


@router.get("/agents/{agent_name}/always-load/{pkid}")
async def get_always_load_item(agent_name: str, pkid: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    node = await pool.fetchrow(
        "SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not node:
        raise HTTPException(status_code=404, detail="Node not found")
    children = await pool.fetch(
        "SELECT pkid, parent_id, title, description FROM always_load WHERE agent_id = $1 AND parent_id = $2 ORDER BY pkid",
        agent["agent_id"], pkid
    )
    return {"node": dict(node), "children": [dict(r) for r in children]}


@router.post("/agents/{agent_name}/always-load")
async def create_always_load(agent_name: str, item: AlwaysLoadCreate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO always_load (agent_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING pkid, parent_id, title",
        agent["agent_id"], item.parent_id, item.title, item.description
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/always-load/{pkid}")
async def update_always_load(agent_name: str, pkid: int, item: AlwaysLoadUpdate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
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


@router.delete("/agents/{agent_name}/always-load/{pkid}")
async def delete_always_load(agent_name: str, pkid: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
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


# ── Memories ─────────────────────────────────────────────────────

@router.get("/agents/{agent_name}/memories")
async def get_memories(agent_name: str, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT pkid, title, description, created_at FROM memories WHERE agent_id = $1 ORDER BY pkid",
        agent["agent_id"]
    )
    return {"agent": agent_name, "memories": [dict(r) for r in rows]}


@router.get("/agents/{agent_name}/memories/{pkid}")
async def get_memory(agent_name: str, pkid: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT pkid, title, description, created_at FROM memories WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not row:
        raise HTTPException(status_code=404, detail="Memory not found")
    return dict(row)


@router.post("/agents/{agent_name}/memories")
async def create_memory(agent_name: str, memory: MemoryCreate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO memories (agent_id, title, description) VALUES ($1, $2, $3) RETURNING pkid, title, created_at",
        agent["agent_id"], memory.title, memory.description
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/memories/{pkid}")
async def update_memory(agent_name: str, pkid: int, memory: MemoryUpdate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
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
async def delete_memory(agent_name: str, pkid: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM memories WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Memory not found")
    return {"deleted": pkid}


# ── Preferences ──────────────────────────────────────────────────

@router.get("/agents/{agent_name}/preferences")
async def get_preferences(agent_name: str, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT pkid, parent_id, title, description FROM preferences WHERE agent_id = $1 ORDER BY parent_id, pkid",
        agent["agent_id"]
    )
    return {"agent": agent_name, "tree": _build_tree(rows)}


@router.get("/agents/{agent_name}/preferences/{pkid}")
async def get_preference(agent_name: str, pkid: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
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
    return {"node": dict(node), "children": [dict(r) for r in children]}


@router.post("/agents/{agent_name}/preferences")
async def create_preference(agent_name: str, pref: PreferenceCreate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO preferences (agent_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING pkid, title",
        agent["agent_id"], pref.parent_id, pref.title, pref.description
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/preferences/{pkid}")
async def update_preference(agent_name: str, pkid: int, pref: PreferenceUpdate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    existing = await pool.fetchrow(
        "SELECT pkid FROM preferences WHERE agent_id = $1 AND pkid = $2",
        agent["agent_id"], pkid
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Preference not found")

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

    updates.append("updated_at = NOW()")
    values.append(agent["agent_id"])
    values.append(pkid)
    sql = f"UPDATE preferences SET {', '.join(updates)} WHERE agent_id = ${idx} AND pkid = ${idx + 1} RETURNING pkid, title"
    row = await pool.fetchrow(sql, *values)
    if not row:
        raise HTTPException(status_code=404, detail="Preference not found")
    return {"updated": dict(row)}


@router.delete("/agents/{agent_name}/preferences/{pkid}")
async def delete_preference(agent_name: str, pkid: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
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


# ── Handoffs ─────────────────────────────────────────────────────

@router.get("/agents/{agent_name}/handoffs")
async def list_handoffs(agent_name: str, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT handoff_id, title, prompt, created_at FROM handoffs WHERE agent_id = $1 AND picked_up_at IS NULL ORDER BY created_at",
        agent["agent_id"]
    )
    return {"agent": agent_name, "handoffs": [dict(r) for r in rows]}


@router.get("/agents/{agent_name}/handoffs/{handoff_id}")
async def get_handoff(agent_name: str, handoff_id: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT handoff_id, title, prompt, created_at, picked_up_at FROM handoffs WHERE agent_id = $1 AND handoff_id = $2",
        agent["agent_id"], handoff_id
    )
    if not row:
        raise HTTPException(status_code=404, detail="Handoff not found")
    return dict(row)


@router.post("/agents/{agent_name}/handoffs")
async def create_handoff(agent_name: str, body: HandoffCreate, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO handoffs (agent_id, title, prompt) VALUES ($1, $2, $3) RETURNING handoff_id, title, created_at",
        agent["agent_id"], body.title, body.prompt
    )
    return {"created": dict(row)}


@router.put("/agents/{agent_name}/handoffs/{handoff_id}/pickup")
async def pickup_handoff(agent_name: str, handoff_id: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "UPDATE handoffs SET picked_up_at = NOW() WHERE agent_id = $1 AND handoff_id = $2 AND picked_up_at IS NULL RETURNING handoff_id, title, picked_up_at",
        agent["agent_id"], handoff_id
    )
    if not row:
        raise HTTPException(status_code=404, detail="Handoff not found or already picked up")
    return {"picked_up": dict(row)}


@router.delete("/agents/{agent_name}/handoffs/{handoff_id}")
async def delete_handoff(agent_name: str, handoff_id: int, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM handoffs WHERE agent_id = $1 AND handoff_id = $2",
        agent["agent_id"], handoff_id
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Handoff not found")
    return {"deleted": handoff_id}


# ── Sessions ─────────────────────────────────────────────────────

@router.get("/agents/{agent_name}/sessions/last")
async def get_last_session(agent_name: str, user: dict = Depends(verify_user_token)):
    agent = await _require_user_agent(agent_name, user)
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT session_id, started_at, project FROM sessions WHERE agent_id = $1 ORDER BY started_at DESC LIMIT 1",
        agent["agent_id"]
    )
    if not row:
        return {"agent": agent_name, "last_session": None}
    return {
        "agent": agent_name,
        "last_session": {
            "session_id": row["session_id"],
            "started_at": row["started_at"].isoformat(),
            "project": row["project"],
        }
    }
