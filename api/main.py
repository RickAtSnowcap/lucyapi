import os
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI, Request, Response
from starlette.routing import Mount
from .database import init_pool, close_pool
from .routes import time, context, memories, sessions, preferences, projects, save, secrets, handoffs, images, google_docs, hints
from .mcp_server import create_mcp_session_manager

logger = logging.getLogger(__name__)

DATABASE_URL = os.environ.get(
    "LUCYAPI_DATABASE_URL",
    "postgresql://lucy@localhost:5432/lucyapi"
)

# Create MCP session manager at module level (stateless, no server-side state)
mcp_session_manager = create_mcp_session_manager()


@asynccontextmanager
async def lifespan(app: FastAPI):
    await init_pool(DATABASE_URL)
    async with mcp_session_manager.run():
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


# ── MCP Streamable HTTP endpoint ─────────────────────────────────
# Mounted as a raw ASGI sub-application at /mcp
# Claude custom connectors point here: https://lucyapi.snowcapsystems.com/mcp

async def mcp_asgi_handler(scope, receive, send):
    """ASGI handler that routes MCP Streamable HTTP requests."""
    await mcp_session_manager.handle_request(scope, receive, send)

app.mount("/mcp", app=mcp_asgi_handler)


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
        "mcp": {
            "endpoint": "https://lucyapi.snowcapsystems.com/mcp",
            "transport": "Streamable HTTP (stateless)",
            "auth": "None (authless connector — agent key baked into server)",
            "tools": 45,
            "note": "Add as custom connector in Claude Settings > Connectors"
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
                "project_sections": "user-scoped",
                "secrets": "user-scoped",
                "handoffs": "agent-scoped (cross-agent read/create, named-agent pickup/delete)",
                "hints": "user-scoped"
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
            ],
            "secrets": [
                {"method": "GET", "path": "/secrets", "auth": True, "description": "List secret keys (names only, no values)"},
                {"method": "GET", "path": "/secrets/{key}", "auth": True, "description": "Get decrypted secret value"},
                {"method": "PUT", "path": "/secrets/{key}", "auth": True, "description": "Create or update a secret (encrypts on write)"},
                {"method": "DELETE", "path": "/secrets/{key}", "auth": True, "description": "Delete a secret"}
            ],
            "handoffs": [
                {"method": "GET", "path": "/agents/{name}/handoffs", "auth": True, "description": "List pending handoffs (picked_up_at IS NULL)"},
                {"method": "GET", "path": "/agents/{name}/handoffs/{id}", "auth": True, "description": "Get a specific handoff"},
                {"method": "POST", "path": "/agents/{name}/handoffs", "auth": True, "description": "Create a handoff (cross-agent OK)"},
                {"method": "PUT", "path": "/agents/{name}/handoffs/{id}/pickup", "auth": True, "description": "Mark as picked up (named agent only)"},
                {"method": "DELETE", "path": "/agents/{name}/handoffs/{id}", "auth": True, "description": "Delete a handoff (named agent only)"}
            ],
            "hints": [
                {"method": "GET", "path": "/hints", "auth": True, "description": "Full hints tree with descriptions"},
                {"method": "GET", "path": "/hints/{hint_id}", "auth": True, "description": "Hint node and its immediate children"},
                {"method": "POST", "path": "/hints", "auth": True, "description": "Create a hint node (user-scoped write)"},
                {"method": "PUT", "path": "/hints/{hint_id}", "auth": True, "description": "Update a hint (user-scoped write)"},
                {"method": "DELETE", "path": "/hints/{hint_id}", "auth": True, "description": "Delete a hint node + descendants (user-scoped write)"}
            ],
            "images": [
                {"method": "POST", "path": "/genimage", "auth": False, "description": "Generate an image from a text prompt via Gemini"},
                {"method": "POST", "path": "/genimage/edit", "auth": False, "description": "Edit an existing image with a text prompt via Gemini"},
                {"method": "POST", "path": "/genimage/analyze", "auth": False, "description": "Analyze an image and return a text description via Gemini"},
                {"method": "GET", "path": "/images", "auth": False, "description": "List images with optional keep filter"},
                {"method": "GET", "path": "/images/{image_id}", "auth": False, "description": "Get metadata for a single image"},
                {"method": "PATCH", "path": "/images/{image_id}", "auth": False, "description": "Update keep flag on an image"},
                {"method": "DELETE", "path": "/images/{image_id}", "auth": False, "description": "Delete an image (rejects if keep=true unless force=true)"},
                {"method": "POST", "path": "/images/cleanup", "auth": False, "description": "Bulk delete all images where keep=false"},
                {"method": "GET", "path": "/nanoimages/{filename}", "auth": False, "description": "Static image file serving via Caddy"}
            ]
        }
    }


app.include_router(time.router)
app.include_router(context.router)
app.include_router(memories.router)
app.include_router(sessions.router)
app.include_router(preferences.router)
app.include_router(projects.router)
app.include_router(save.router)
app.include_router(secrets.router)
app.include_router(handoffs.router)
app.include_router(images.router)
app.include_router(google_docs.router)
app.include_router(hints.router)
