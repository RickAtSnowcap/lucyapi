import os
import bcrypt
import jwt
from datetime import datetime, timezone, timedelta
from fastapi import Header, HTTPException
from typing import Optional
from .database import get_pool

# JWT config
JWT_SECRET = os.environ.get("LUCYAPI_JWT_SECRET", "change-me-in-production")
JWT_ALGORITHM = "HS256"
JWT_EXPIRATION_HOURS = 24


def hash_password(password: str) -> str:
    return bcrypt.hashpw(password.encode(), bcrypt.gensalt()).decode()


def verify_password(password: str, password_hash: str) -> bool:
    return bcrypt.checkpw(password.encode(), password_hash.encode())


def create_token(user_id: int, username: str) -> str:
    payload = {
        "user_id": user_id,
        "username": username,
        "exp": datetime.now(timezone.utc) + timedelta(hours=JWT_EXPIRATION_HOURS),
        "iat": datetime.now(timezone.utc),
    }
    return jwt.encode(payload, JWT_SECRET, algorithm=JWT_ALGORITHM)


def decode_token(token: str) -> dict:
    try:
        return jwt.decode(token, JWT_SECRET, algorithms=[JWT_ALGORITHM])
    except jwt.ExpiredSignatureError:
        raise HTTPException(status_code=401, detail="Token expired")
    except jwt.InvalidTokenError:
        raise HTTPException(status_code=401, detail="Invalid token")


async def verify_user_token(authorization: Optional[str] = Header(None)) -> dict:
    """FastAPI dependency — validates JWT Bearer token, returns user info."""
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="Bearer token required")

    token = authorization[7:]  # strip "Bearer "
    payload = decode_token(token)

    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT user_id, name, username, email FROM users WHERE user_id = $1",
        payload["user_id"]
    )
    if not row:
        raise HTTPException(status_code=401, detail="User not found")
    return dict(row)
