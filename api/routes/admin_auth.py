from fastapi import APIRouter, HTTPException, Depends
from pydantic import BaseModel
from ..database import get_pool
from ..user_auth import hash_password, verify_password, create_token, verify_user_token

router = APIRouter(prefix="/auth", tags=["auth"])


class LoginRequest(BaseModel):
    username: str
    password: str


class TokenResponse(BaseModel):
    token: str
    user_id: int
    username: str
    name: str


@router.post("/login", response_model=TokenResponse)
async def login(req: LoginRequest):
    pool = await get_pool()
    row = await pool.fetchrow(
        "SELECT user_id, name, username, password_hash FROM users WHERE username = $1",
        req.username
    )
    if not row or not row["password_hash"]:
        raise HTTPException(status_code=401, detail="Invalid credentials")
    if not verify_password(req.password, row["password_hash"]):
        raise HTTPException(status_code=401, detail="Invalid credentials")

    token = create_token(row["user_id"], row["username"])
    return TokenResponse(
        token=token,
        user_id=row["user_id"],
        username=row["username"],
        name=row["name"]
    )


@router.post("/refresh", response_model=TokenResponse)
async def refresh(user: dict = Depends(verify_user_token)):
    token = create_token(user["user_id"], user["username"])
    return TokenResponse(
        token=token,
        user_id=user["user_id"],
        username=user["username"],
        name=user["name"]
    )


@router.get("/me")
async def get_me(user: dict = Depends(verify_user_token)):
    return {
        "user_id": user["user_id"],
        "name": user["name"],
        "username": user["username"],
        "email": user.get("email")
    }
