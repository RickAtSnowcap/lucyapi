# LucyAPI

Multi-user, multi-agent context service for AI assistants.

## Overview

LucyAPI provides persistent memory and temporal grounding for AI assistants. It serves as the context backbone that makes each conversation feel informed by shared history, rather than starting from scratch.

## Stack

- **Runtime:** Python 3.12, FastAPI, uvicorn
- **Database:** PostgreSQL 16 (asyncpg)
- **Reverse Proxy:** Caddy (auto Let's Encrypt SSL)
- **Host:** Ubuntu Server 24.04 LTS

## Architecture

### Data Scoping

- **Agent-scoped:** Data owned by a specific agent. Only that agent can write; all agents for the same user can read. (always_load, memories, preferences, sessions)
- **User-scoped:** Data owned by the user. Any of the user's agents can read and write. (projects, project_sections)

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
psql -d lucyapi -f database/002-seed-data.sql
```

### Environment

```bash
export LUCYAPI_DATABASE_URL="postgresql://lucy@localhost:5432/lucyapi"
```

## License

Proprietary — Snowcap Systems LLC
