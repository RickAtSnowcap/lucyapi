from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from ..auth import verify_api_key
from ..database import get_pool
from ..encryption import encrypt, decrypt

router = APIRouter()


class SecretCreate(BaseModel):
    value: str


@router.get("/secrets")
async def list_secrets(caller: dict = Depends(verify_api_key)):
    """List secret keys (names only, no values)."""
    pool = await get_pool()
    rows = await pool.fetch(
        "SELECT key, created_at, updated_at FROM secrets WHERE user_id = $1 ORDER BY key",
        caller["user_id"]
    )
    return {"secrets": [dict(r) for r in rows]}


@router.get("/secrets/{key}")
async def get_secret(key: str, caller: dict = Depends(verify_api_key)):
    """Get decrypted value for a secret."""
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT secret_id, key, encrypted_value, created_at, updated_at FROM secrets WHERE user_id = $1 AND key = $2",
        caller["user_id"], key
    )
    if not row:
        raise HTTPException(status_code=404, detail="Secret not found")
    return {"key": row["key"], "value": decrypt(row["encrypted_value"])}


@router.put("/secrets/{key}")
async def set_secret(key: str, body: SecretCreate, caller: dict = Depends(verify_api_key)):
    """Create or update a secret (encrypts on write)."""
    pool = await get_pool()
    encrypted = encrypt(body.value)
    row = await pool.fetchrow(
        """
        INSERT INTO secrets (user_id, key, encrypted_value)
        VALUES ($1, $2, $3)
        ON CONFLICT ON CONSTRAINT uq_secrets_user_key
        DO UPDATE SET encrypted_value = EXCLUDED.encrypted_value, updated_at = NOW()
        RETURNING secret_id, key, created_at, updated_at
        """,
        caller["user_id"], key, encrypted
    )
    return {"saved": dict(row)}


@router.delete("/secrets/{key}")
async def delete_secret(key: str, caller: dict = Depends(verify_api_key)):
    """Delete a secret."""
    pool = await get_pool()
    result = await pool.execute(
        "DELETE FROM secrets WHERE user_id = $1 AND key = $2",
        caller["user_id"], key
    )
    if result == "DELETE 0":
        raise HTTPException(status_code=404, detail="Secret not found")
    return {"deleted": key}
