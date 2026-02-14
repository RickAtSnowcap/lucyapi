from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from typing import Optional
from ..auth import verify_api_key
from ..database import get_pool
from .sharing import check_share_permission

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
    """Full hints tree with descriptions, including shared hint categories."""
    pool = await get_pool()
    rows = await pool.fetch(
        """SELECT hint_id, parent_id, title, description, hint_category_id, 'owned' as access, 3 as permission_level
           FROM hints WHERE user_id = $1
           UNION ALL
           SELECT h.hint_id, h.parent_id, h.title, h.description, h.hint_category_id, 'shared' as access, so.permission_level
           FROM hints h
           JOIN shared_objects so ON so.object_id = h.hint_category_id AND so.object_type_id = 2
           WHERE so.shared_to_user_id = $1
           ORDER BY parent_id, hint_id""",
        caller["user_id"]
    )
    return {"hints": [dict(r) for r in rows]}


@router.get("/hints/{hint_id}")
async def get_hint(hint_id: int, caller: dict = Depends(verify_api_key)):
    """A hint node and its immediate children."""
    pool = await get_pool()
    node = await pool.fetchrow(
        "SELECT hint_id, user_id, parent_id, title, description, hint_category_id FROM hints WHERE hint_id = $1",
        hint_id
    )
    if not node:
        raise HTTPException(status_code=404, detail="Hint not found")

    if node["user_id"] != caller["user_id"]:
        perm = await check_share_permission(pool, caller["user_id"], 2, node["hint_category_id"], 1)
        if not perm:
            raise HTTPException(status_code=404, detail="Hint not found")

    children = await pool.fetch(
        "SELECT hint_id, parent_id, title, description, hint_category_id FROM hints WHERE parent_id = $1 ORDER BY hint_id",
        hint_id
    )
    result = dict(node)
    del result["user_id"]
    return {"node": result, "children": [dict(r) for r in children]}


@router.post("/hints")
async def create_hint(hint: HintCreate, caller: dict = Depends(verify_api_key)):
    """Create a hint node. User approval enforced by agent behavior."""
    pool = await get_pool()

    if hint.parent_id == 0:
        # New root category — must be the caller's own
        row = await pool.fetchrow(
            "INSERT INTO hints (user_id, parent_id, title, description, hint_category_id) VALUES ($1, 0, $2, $3, 0) RETURNING hint_id, parent_id, title",
            caller["user_id"], hint.title, hint.description
        )
        # Set hint_category_id to the newly created hint_id
        await pool.execute(
            "UPDATE hints SET hint_category_id = $1 WHERE hint_id = $1",
            row["hint_id"]
        )
        result = dict(row)
        result["hint_category_id"] = row["hint_id"]
        return {"created": result}
    else:
        # Child hint — look up parent to get hint_category_id
        parent = await pool.fetchrow(
            "SELECT hint_id, user_id, hint_category_id FROM hints WHERE hint_id = $1",
            hint.parent_id
        )
        if not parent:
            raise HTTPException(status_code=404, detail="Parent hint not found")

        # Check ownership or shared permission >= 2
        if parent["user_id"] != caller["user_id"]:
            perm = await check_share_permission(pool, caller["user_id"], 2, parent["hint_category_id"], 2)
            if not perm:
                raise HTTPException(status_code=404, detail="Parent hint not found")

        row = await pool.fetchrow(
            "INSERT INTO hints (user_id, parent_id, title, description, hint_category_id) VALUES ($1, $2, $3, $4, $5) RETURNING hint_id, parent_id, title, hint_category_id",
            parent["user_id"], hint.parent_id, hint.title, hint.description, parent["hint_category_id"]
        )
        return {"created": dict(row)}


@router.put("/hints/{hint_id}")
async def update_hint(hint_id: int, hint: HintUpdate, caller: dict = Depends(verify_api_key)):
    """Update a hint node. User approval enforced by agent behavior."""
    pool = await get_pool()

    # Check ownership or shared permission >= 2
    existing = await pool.fetchrow(
        "SELECT hint_id, user_id, hint_category_id FROM hints WHERE hint_id = $1",
        hint_id
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Hint not found")

    if existing["user_id"] != caller["user_id"]:
        perm = await check_share_permission(pool, caller["user_id"], 2, existing["hint_category_id"], 2)
        if not perm:
            raise HTTPException(status_code=404, detail="Hint not found")

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
    values.append(hint_id)
    sql = f"UPDATE hints SET {', '.join(updates)} WHERE hint_id = ${idx} RETURNING hint_id, title"
    row = await pool.fetchrow(sql, *values)
    return {"updated": dict(row)}


@router.delete("/hints/{hint_id}")
async def delete_hint(hint_id: int, caller: dict = Depends(verify_api_key)):
    """Delete a hint node and all descendants. User approval enforced by agent behavior."""
    pool = await get_pool()

    existing = await pool.fetchrow(
        "SELECT hint_id, user_id, parent_id, hint_category_id FROM hints WHERE hint_id = $1",
        hint_id
    )
    if not existing:
        raise HTTPException(status_code=404, detail="Hint not found")

    if existing["user_id"] != caller["user_id"]:
        if existing["parent_id"] == 0:
            # Root hints: owner only
            raise HTTPException(status_code=404, detail="Hint not found")
        perm = await check_share_permission(pool, caller["user_id"], 2, existing["hint_category_id"], 3)
        if not perm:
            raise HTTPException(status_code=404, detail="Hint not found")

    result = await pool.execute(
        """
        WITH RECURSIVE subtree AS (
            SELECT hint_id FROM hints WHERE hint_id = $1
            UNION ALL
            SELECT h.hint_id FROM hints h
            INNER JOIN subtree s ON h.parent_id = s.hint_id
        )
        DELETE FROM hints WHERE hint_id IN (SELECT hint_id FROM subtree)
        """,
        hint_id
    )
    deleted_count = int(result.split()[-1])
    return {"deleted": hint_id, "descendants_deleted": deleted_count - 1}
