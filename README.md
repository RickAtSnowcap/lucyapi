# LucyAPI v2

Multi-agent context service for AI assistants. Built with .NET 10 Native AOT — compiles to a single ~21MB binary with no runtime dependency.

## What it does

LucyAPI provides persistent, structured context that AI agents can load on demand during conversations. Instead of stuffing everything into a system prompt, agents call the API to fetch exactly what they need — memories, preferences, project notes, wiki documentation, hints, secrets, and more.

The core idea: **agents shouldn't lose context between sessions, and they shouldn't carry context they don't need.**

### Key features

- **Always Load** — context that loads automatically at session start (per-agent)
- **Memories** — persistent agent memories, organized by topic
- **Preferences** — user preferences agents should respect
- **Projects** — hierarchical project tracking with nested sections
- **Wikis** — structured documentation with nested sections and tagging
- **Hints** — categorized hints and knowledge base entries with access control
- **Secrets** — encrypted key-value storage (BYTEA at rest in PostgreSQL)
- **Sharing** — cross-user object sharing with permission levels
- **Handoffs** — structured task handoffs between agents
- **Sessions** — automatic: every `get_context` call opens a session; projects an agent loads (`get_project` / `get_project_compact`) are recorded as that session's focus
- **Nudges** — deadline reminders and actionable items with frequency control
- **Images** — AI image generation, editing, analysis, and lifecycle management
- **Google Docs** — Google Drive and Docs integration (create, read, update, move, delete)
- **Save Notes** — email markdown notes from mobile or external contexts
- **Boot** — agent bootstrap endpoint returning API URLs and always-load context
- **Admin** — JWT-authenticated management of agents, resources, and auth

## Architecture

```
LucyAPI.Api          Endpoints, middleware, JSON serialization (AOT source-generated)
LucyAPI.Services     Business logic, interfaces, DTOs, tree building
LucyAPI.Data         Repositories, models — all DB access via PL/pgSQL functions
```

Three-layer architecture with constructor-injected singletons. All database operations call PL/pgSQL functions in a `lucyapi` schema — no raw SQL, no ORM, no Entity Framework.

### AOT constraints

This is a fully Native AOT application. That means:
- **No reflection** — all JSON serialization is source-generated via `AppJsonSerializerContext`
- **No anonymous types** in API responses — every response shape has a typed wrapper
- **No dynamic code generation** — Npgsql uses `NpgsqlSlimDataSourceBuilder`
- **All repository reads are ordinal-based** — `reader.GetInt32(0)`, not `reader["column_name"]`

### Authentication

Agents reach LucyAPI only through the MCP connector at `/mcp/connector`, authenticated by an OAuth bearer token (LucyAPI is its own authorization server: `/.well-known/*`, `/oauth/*`). The token names one agent + user identity. LucyAdmin uses JWT (`/auth/*`, `/admin/*`). Public endpoints: `/health`, `/time`, and the signed, 24-hour document links `/doc/projects/{id}` and `/doc/wikis/{id}`.

### Database

PostgreSQL with all business logic in PL/pgSQL functions under the `lucyapi` schema. Tables live in `public`. Connection string is AES-256-CBC encrypted at rest using [tcrypt-lib](https://github.com/RickAtSnowcap/tcrypt-lib) with a TPM-sealed key delivered via systemd credentials.

## API endpoints

| Group | Endpoints |
|-------|-----------|
| Utilities | `GET /health`, `GET /time` |
| MCP connector | `POST/GET/DELETE /mcp/connector` (JSON-RPC: `initialize`, `tools/list`, `tools/call`) |
| OAuth | `GET /.well-known/oauth-protected-resource[/mcp/connector]`, `GET /.well-known/oauth-authorization-server`, `POST /oauth/register`, `GET/POST /oauth/authorize`, `POST /oauth/token` |
| Documents | `GET /doc/projects/{id}`, `GET /doc/wikis/{id}` (signed links from `get_project` / `get_wiki`) |
| Admin auth | `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me`, `PUT /auth/password` |
| Admin | `/admin/agents/{name}/...`, `/admin/projects`, `/admin/wikis`, `/admin/hints`, `/admin/hint-categories`, `/admin/secrets`, `/admin/sharing`, `/admin/nudges`, `/admin/images`, `/admin/users`, `/admin/project-statuses`, `/admin/dashboard` |

## Running

### Development

```bash
export LUCYAPI_CONNECTION_STRING="Host=...;Database=...;Username=...;Password=..."
dotnet run --project src/LucyAPI.Api
```

### Production (Native AOT)

```bash
dotnet publish src/LucyAPI.Api -c Release
# Output: single native binary (~21MB), no .NET runtime required
```

The production binary reads its database connection string from `appsettings.json` (`Suitcase:DbConnection`), encrypted with tcrypt-lib. The decryption key is TPM-sealed and delivered at service start via `systemd LoadCredentialEncrypted=`.

## Dependencies

- [Npgsql](https://www.npgsql.org/) — PostgreSQL driver (AOT-safe slim builder)
- [tcrypt-lib](https://github.com/RickAtSnowcap/tcrypt-lib) — AES-256-GCM encryption with TPM key delivery

## License

MIT
