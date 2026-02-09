import asyncpg

_pool = None

async def get_pool() -> asyncpg.Pool:
    global _pool
    if _pool is None:
        raise RuntimeError("Database pool not initialized")
    return _pool

async def init_pool(dsn: str, min_size: int = 2, max_size: int = 10):
    global _pool
    _pool = await asyncpg.create_pool(dsn, min_size=min_size, max_size=max_size)

async def close_pool():
    global _pool
    if _pool:
        await _pool.close()
        _pool = None
