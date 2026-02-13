from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


class HintCreate(BaseModel):
    parent_id: int = 0
    title: str
    description: Optional[str] = None


class HintUpdate(BaseModel):
    title: Optional[str] = None
    description: Optional[str] = None


@router.get("/hints")
async def get_hints(caller: dict = Depends(verify_api_key)):
    """Full hints tree with descriptions."""
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT hint_id, parent_id, title, description FROM hints WHERE user_id = $1 ORDER BY parent_id, hint_id",
        caller["user_id"]
    )
    return {"hints": [dict(r) for r in rows]}


@router.get("/hints/{hint_id}")
async def get_hint(hint_id: int, caller: dict = Depends(verify_api_key)):
    """A hint node and its immediate children."""
    pool = await get_pool()
    node = await pool.fetchrow(
        "SELECT hint_id, parent_id, title, description FROM hints WHERE user_id = $1 AND hint_id = $2",
        caller["user_id"], hint_id
    )
    if not node:
        raise HTTPException(status_code=404, detail="Hint not found")

    children = await pool.fetch(
        "SELECT hint_id, parent_id, title, description FROM hints WHERE user_id = $1 AND parent_id = $2 ORDER BY hint_id",
        caller["user_id"], hint_id
    )
    return {"node": dict(node), "children": [dict(r) for r in children]}


@router.post("/hints")
async def create_hint(hint: HintCreate, caller: dict = Depends(verify_api_key)):
    """Create a hint node. User approval enforced by agent behavior."""
    pool = await get_pool()
    row = await pool.fetchrow(
        "INSERT INTO hints (user_id, parent_id, title, description) VALUES ($1, $2, $3, $4) RETURNING hint_id, parent_id, title",
        caller["user_id"], hint.parent_id, hint.title, hint.description
    )
    return {"created": dict(row)}


@router.put("/hints/{hint_id}")
async def update_hint(hint_id: int, hint: HintUpdate, caller: dict = Depends(verify_api_key)):
    """Update a hint node. User approval enforced by agent behavior."""
    pool = await get_pool()
    updates = []
    values = []
    idx = 1
    if hint.title is not None:
        updates.append(f"title = ${idx}")
        values.append(hint.title)
        idx += 1
    if hint.description is not None:
        updates.append(f"description = ${idx}")
        values.append(hint.description)
        idx += 1

    if not updates:
        raise HTTPException(status_code=400, detail="No fields to update")

    updates.append("updated_at = NOW()")
    values.extend([caller["user_id"], hint_id])
    sql = f"UPDATE hints SET {', '.join(updates)} WHERE user_id = ${idx} AND hint_id = ${idx + 1} RETURNING hint_id, title"
    row = await pool.fetchrow(sql, *values)
    if not row:
        raise HTTPException(status_code=404, detail="Hint not found")
    return {"updated": dict(row)}


@router.delete("/hints/{hint_id}")
async def delete_hint(hint_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a hint node and all descendants. User approval enforced by agent behavior."""
    pool = await get_pool()
    result = await pool.execute(
        """
        WITH RECURSIVE subtree AS (
            SELECT hint_id FROM hints WHERE user_id = $1 AND hint_id = $2
            UNION ALL
            SELECT h.hint_id FROM hints h
            INNER JOIN subtree s ON h.parent_id = s.hint_id
            WHERE h.user_id = $1
        )
        DELETE FROM hints WHERE hint_id IN (SELECT hint_id FROM subtree)
        """,
        caller["user_id"], hint_id
    )
    deleted_count = int(result.split()[-1])
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Hint not found")
    return {"deleted": hint_id, "descendants_deleted": deleted_count - 1}
