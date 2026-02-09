from fastapi import Header, HTTPException, Query
from typing import Optional
from .database import get_pool


async def verify_api_key(
    x_api_key: Optional[str] = Header(None),
    agent_key: Optional[str] = Query(None)
) -> dict:
    """Validate API key and return agent info.
    
    Accepts key from either X-Api-Key header or agent_key query parameter.
    Header takes precedence if both are provided.
    """
    api_key = x_api_key or agent_key
    if not api_key:
        raise HTTPException(status_code=401, detail="API key required (X-Api-Key header or agent_key parameter)")
    
    pool = await get_pool()
    row = await pool.fetchrow(
        """
        SELECT a.agent_id, a.name AS agent_name, a.user_id, u.name AS user_name
        FROM agents a
        JOIN users u ON a.user_id = u.user_id
        WHERE a.api_key = $1
        """,
        api_key
    )
    if not row:
        raise HTTPException(status_code=401, detail="Invalid API key")
    return dict(row)
