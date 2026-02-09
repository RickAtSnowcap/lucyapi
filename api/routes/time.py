from datetime import datetime
from zoneinfo import ZoneInfo
from fastapi import APIRouter
from ..database import get_pool

router = APIRouter()

TIMEZONE = ZoneInfo("America/Denver")


@router.get("/time")
async def get_time():
    now_utc = datetime.now(ZoneInfo("UTC"))
    now_local = now_utc.astimezone(TIMEZONE)
    return {
        "utc": now_utc.isoformat(),
        "local": now_local.isoformat(),
        "tz": "America/Denver",
        "day": now_local.strftime("%A")
    }


@router.get("/health")
async def get_health():
    try:
        pool = await get_pool()
        row = await pool.fetchrow("SELECT 1 AS ok")
        db_status = "connected" if row else "error"
    except Exception:
        db_status = "error"

    return {
        "status": "ok" if db_status == "connected" else "degraded",
        "database": db_status
    }
