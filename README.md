# LucyAPI

Multi-user, multi-agent context service for AI assistants.

## Overview

LucyAPI provides persistent memory, temporal grounding, and tool integration for AI assistants. It serves as the context backbone that makes each conversation feel informed by shared history, rather than starting from scratch.

## Stack

- **Runtime:** Python 3.12, FastAPI, uvicorn
- **Database:** PostgreSQL 16 (asyncpg)
- **Reverse Proxy:** Caddy (auto Let's Encrypt SSL)
- **Host:** Ubuntu Server 24.04 LTS
- **MCP:** Streamable HTTP (stateless) at `/mcp`
- **Image Generation:** Gemini API (google-genai)
- **Documents:** Google Docs/Drive API (OAuth2)
- **Encryption:** AES-256-GCM (cryptography)

## Architecture

### Data Scoping

- **Agent-scoped:** Data owned by a specific agent. Only that agent can write; all agents for the same user can read. (always_load, memories, preferences, sessions, handoffs)
- **User-scoped:** Data owned by the user. Any of the user's agents can read and write. (projects, project_sections, secrets, hints)

### MCP Server

LucyAPI exposes all endpoints as MCP tools via Streamable HTTP at `/mcp`. Claude custom connectors (including mobile) can interact with the full API. Each agent authenticates by passing its `agent_key` parameter on every tool call.

### Endpoints

Hit `GET /` for the full API manifest with all endpoints and descriptions.

## Setup

```bash
python -m venv venv
source venv/bin/activate
pip install -r requirements.txt
uvicorn api.main:app --host 127.0.0.1 --port 8100
```

### Database

Create the `lucyapi` database in PostgreSQL, then run the migration scripts in order:

```bash
psql -d lucyapi -f database/001-create-tables.sql
psql -d lucyapi -f database/002-secrets-handoffs.sql
```

### Environment

```bash
export LUCYAPI_DATABASE_URL="postgresql://lucy@localhost:5432/lucyapi"
export GEMINI_API_KEY="your-gemini-api-key"
export LUCYAPI_SECRETS_KEY="/opt/lucyapi/keys/secrets.key"
export LUCYAPI_SAVE_TOKEN="your-save-token"
export LUCYAPI_SMTP_PASS="your-smtp-password"
```

### Encryption Key

Generate a 32-byte AES key for secrets encryption:

```bash
mkdir -p keys
python -c "import os; open('keys/secrets.key', 'wb').write(os.urandom(32))"
chmod 600 keys/secrets.key
```

## License

Proprietary — Snowcap Systems LLC
