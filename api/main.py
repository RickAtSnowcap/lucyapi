import os
from contextlib import asynccontextmanager
from fastapi import FastAPI, Request, Response
from .database import init_pool, close_pool
from .routes import time, context, memories, sessions, preferences, projects

DATABASE_URL = os.environ.get(
    "LUCYAPI_DATABASE_URL",
    "postgresql://lucy@localhost:5432/lucyapi"
)


@asynccontextmanager
async def lifespan(app: FastAPI):
    await init_pool(DATABASE_URL)
    yield
    await close_pool()


app = FastAPI(
    title="LucyAPI",
    description="Multi-user, multi-agent context service for Snowcap Systems",
    version="1.0.0",
    lifespan=lifespan
)

@app.middleware("http")
async def no_cache(request: Request, call_next):
    response = await call_next(request)
    response.headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0"
    response.headers["Pragma"] = "no-cache"
    return response


@app.get("/")
async def api_root():
    """API manifest — describes all available endpoints."""
    return {
        "service": "LucyAPI",
        "version": "1.0.0",
        "description": "Multi-user, multi-agent context service for Snowcap Systems",
        "base_url": "https://lucyapi.snowcapsystems.com",
        "auth": {
            "method": "API key via X-Api-Key header",
            "note": "Agents authenticate with their unique key from the agents table"
        },
        "access_model": {
            "agent_scoped": "Data owned by a specific agent. Only that agent's API key can write. All agents for the same user can read.",
            "user_scoped": "Data owned by the user. Any of the user's agents can read and write.",
            "tables": {
                "always_load": "agent-scoped",
                "memories": "agent-scoped",
                "preferences": "agent-scoped",
                "sessions": "agent-scoped",
                "projects": "user-scoped",
                "project_sections": "user-scoped"
            }
        },
        "endpoints": {
            "system": [
                {"method": "GET", "path": "/", "auth": False, "description": "This manifest — all available endpoints"},
                {"method": "GET", "path": "/time", "auth": False, "description": "Current UTC/local timestamps and day of week"},
                {"method": "GET", "path": "/health", "auth": False, "description": "Database connectivity check"}
            ],
            "context": [
                {"method": "GET", "path": "/agents/{name}/context", "auth": True, "description": "Full context payload — always_load titles, memory titles, preferences manifest, project manifest"}
            ],
            "always_load": [
                {"method": "GET", "path": "/agents/{name}/context/always_load", "auth": True, "description": "Full always_load tree with descriptions — core identity and standards"},
                {"method": "GET", "path": "/agents/{name}/context/always_load/{pkid}", "auth": True, "description": "Single always_load node + its children"},
                {"method": "POST", "path": "/agents/{name}/context/always_load", "auth": True, "description": "Create an always_load node (agent-scoped write)"},
                {"method": "PUT", "path": "/agents/{name}/context/always_load/{pkid}", "auth": True, "description": "Update an always_load node (agent-scoped write)"},
                {"method": "DELETE", "path": "/agents/{name}/context/always_load/{pkid}", "auth": True, "description": "Delete an always_load node + children (agent-scoped write)"}
            ],
            "memories": [
                {"method": "GET", "path": "/agents/{name}/memories", "auth": True, "description": "All memories with full descriptions"},
                {"method": "GET", "path": "/agents/{name}/memories/{pkid}", "auth": True, "description": "Single memory"},
                {"method": "POST", "path": "/agents/{name}/memories", "auth": True, "description": "Create a memory (agent-scoped write)"},
                {"method": "PUT", "path": "/agents/{name}/memories/{pkid}", "auth": True, "description": "Update a memory (agent-scoped write)"},
                {"method": "DELETE", "path": "/agents/{name}/memories/{pkid}", "auth": True, "description": "Delete a memory (agent-scoped write)"}
            ],
            "preferences": [
                {"method": "GET", "path": "/agents/{name}/preferences", "auth": True, "description": "Top-level preference categories (manifest)"},
                {"method": "GET", "path": "/agents/{name}/preferences/{pkid}", "auth": True, "description": "Preference node + immediate children"},
                {"method": "POST", "path": "/agents/{name}/preferences", "auth": True, "description": "Create a preference node (agent-scoped write)"},
                {"method": "PUT", "path": "/agents/{name}/preferences/{pkid}", "auth": True, "description": "Update a preference (agent-scoped write)"},
                {"method": "DELETE", "path": "/agents/{name}/preferences/{pkid}", "auth": True, "description": "Delete a preference (agent-scoped write)"}
            ],
            "sessions": [
                {"method": "POST", "path": "/sessions", "auth": True, "description": "Log a session start (agent inferred from API key)"},
                {"method": "GET", "path": "/sessions/last", "auth": True, "description": "Most recent session for the calling agent"}
            ],
            "projects": [
                {"method": "GET", "path": "/projects", "auth": True, "description": "All projects for the caller's user (user-scoped read)"},
                {"method": "GET", "path": "/projects/{project_id}", "auth": True, "description": "Project header + section tree (user-scoped read)"},
                {"method": "GET", "path": "/projects/{project_id}/sections/{section_id}", "auth": True, "description": "Section node + immediate children (user-scoped read)"},
                {"method": "POST", "path": "/projects", "auth": True, "description": "Create a project (user-scoped write)"},
                {"method": "POST", "path": "/projects/{project_id}/sections", "auth": True, "description": "Create a section under a project (user-scoped write)"},
                {"method": "PUT", "path": "/projects/{project_id}", "auth": True, "description": "Update project header (user-scoped write)"},
                {"method": "PUT", "path": "/projects/{project_id}/sections/{section_id}", "auth": True, "description": "Update a section (user-scoped write)"},
                {"method": "DELETE", "path": "/projects/{project_id}", "auth": True, "description": "Delete a project + all sections (user-scoped write)"},
                {"method": "DELETE", "path": "/projects/{project_id}/sections/{section_id}", "auth": True, "description": "Delete a section + descendants (user-scoped write)"}
            ]
        }
    }


app.include_router(time.router)
app.include_router(context.router)
app.include_router(memories.router)
app.include_router(sessions.router)
app.include_router(preferences.router)
app.include_router(projects.router)
