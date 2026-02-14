from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from ..auth import verify_api_key
from ..database import get_pool

router = APIRouter()


# ---------------------------------------------------------------------------
# Permission helper (importable by other route modules)
# ---------------------------------------------------------------------------

async def check_share_permission(pool, user_id: int, object_type_id: int, object_id: int, required_level: int) -> int | None:
    """Check if user has shared access to an object.
    Returns the permission_level if granted, None if no share exists.
    Only returns the permission if it meets or exceeds required_level."""
    row = await pool.fetchrow(
        "SELECT permission_level FROM shared_objects WHERE shared_to_user_id = $1 AND object_type_id = $2 AND object_id = $3 AND permission_level >= $4",
        user_id, object_type_id, object_id, required_level
    )
    return row["permission_level"] if row else None


# ---------------------------------------------------------------------------
# Models
# ---------------------------------------------------------------------------

class ShareCreate(BaseModel):
    shared_to_user_id: int
    object_type_id: int  # 1=project, 2=hint, 3=wiki
    object_id: int
    permission_level: int = 1  # 1=read, 2=read+edit, 3=full


# ---------------------------------------------------------------------------
# Object type -> table/column mapping for ownership verification
# ---------------------------------------------------------------------------

_OBJECT_TABLES = {
    1: ("projects", "project_id"),
    2: ("hints", "hint_id"),
    3: ("wikis", "wiki_id"),
}


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------

@router.post("/sharing")
async def create_share(share: ShareCreate, caller: dict = Depends(verify_api_key)):
    """Share an object with another user. Only the object owner can share."""
    pool = await get_pool()

    if share.object_type_id not in _OBJECT_TABLES:
        raise HTTPException(status_code=400, detail="object_type_id must be 1 (project), 2 (hint), or 3 (wiki)")

    if share.permission_level not in (1, 2, 3):
        raise HTTPException(status_code=400, detail="permission_level must be 1, 2, or 3")

    if share.shared_to_user_id == caller["user_id"]:
        raise HTTPException(status_code=400, detail="Cannot share to yourself")

    # Verify target user exists
    target_user = await pool.fetchrow(
        "SELECT user_id FROM users WHERE user_id = $1",
        share.shared_to_user_id
    )
    if not target_user:
        raise HTTPException(status_code=404, detail="Target user not found")

    # Verify caller owns the object
    table, pk_col = _OBJECT_TABLES[share.object_type_id]
    owner = await pool.fetchrow(
        f"SELECT {pk_col} FROM {table} WHERE {pk_col} = $1 AND user_id = $2",
        share.object_id, caller["user_id"]
    )
    if not owner:
        raise HTTPException(status_code=404, detail="Object not found or you are not the owner")

    # Upsert: insert or update permission_level on conflict
    row = await pool.fetchrow(
        """INSERT INTO shared_objects (shared_by_user_id, shared_to_user_id, object_type_id, object_id, permission_level)
           VALUES ($1, $2, $3, $4, $5)
           ON CONFLICT ON CONSTRAINT uq_shared_objects
           DO UPDATE SET permission_level = EXCLUDED.permission_level
           RETURNING share_id, object_type_id, object_id, shared_to_user_id, permission_level""",
        caller["user_id"], share.shared_to_user_id, share.object_type_id, share.object_id, share.permission_level
    )
    return {"shared": dict(row)}


@router.delete("/sharing/{share_id}")
async def revoke_share(share_id: int, caller: dict = Depends(verify_api_key)):
    """Revoke a share. Only the user who shared it can revoke."""
    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM shared_objects WHERE share_id = $1 AND shared_by_user_id = $2",
        share_id, caller["user_id"]
    )
    deleted_count = int(result.split()[-1])
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Share not found or you are not the owner")
    return {"revoked": share_id}


@router.get("/sharing/by-me")
async def shares_by_me(caller: dict = Depends(verify_api_key)):
    """List objects I've shared out."""
    pool = await get_pool()
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
    return {"shares": [dict(r) for r in rows]}


@router.get("/sharing/to-me")
async def shares_to_me(caller: dict = Depends(verify_api_key)):
    """List objects shared to me."""
    pool = await get_pool()
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
    return {"shares": [dict(r) for r in rows]}
