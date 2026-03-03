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
- **Sessions** — session tracking and notes

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

API key auth via `X-Api-Key` header or `agent_key` query parameter. Each key maps to an agent + user identity. Public endpoints: `/health`, `/time`.

### Database

PostgreSQL with all business logic in PL/pgSQL functions under the `lucyapi` schema. Tables live in `public`. Connection string is AES-256-CBC encrypted at rest using [tcrypt-lib](https://github.com/RickAtSnowcap/tcrypt-lib) with a TPM-sealed key delivered via systemd credentials.

## API endpoints

| Group | Endpoints |
|-------|-----------|
| Utilities | `GET /health`, `GET /time` |
| Context | `GET /context` |
| Always Load | `GET/POST/PUT/DELETE /agents/{name}/always-load[/{id}]` |
| Memories | `GET/POST/PUT/DELETE /agents/{name}/memories[/{id}]` |
| Preferences | `GET/POST/PUT/DELETE /agents/{name}/preferences[/{id}]` |
| Projects | `GET /project-statuses`, `GET/POST/PUT/DELETE /projects[/{id}]` |
| Sections | `GET/POST/PUT/DELETE /projects/{id}/sections[/{id}]` |
| Wikis | `GET/POST/PUT/DELETE /wikis[/{id}]`, `GET /wikis/{id}/document` |
| Wiki Sections | `GET/POST/PUT/DELETE /wikis/{id}/sections[/{id}]` |
| Wiki Tags | `GET /wikis/{id}/tags`, `GET /wikis/tags/{tag}` |
| Hints | `GET/POST/PUT/DELETE /hints[/{id}]`, `POST /hints/categories` |
| Secrets | `GET/PUT/DELETE /secrets[/{key}]` |
| Sharing | `POST/DELETE /sharing`, `GET /sharing/by-me`, `GET /sharing/to-me`, `GET /sharing/check` |
| Handoffs | `GET/POST/DELETE /agents/{name}/handoffs[/{id}]`, `PUT .../pickup` |
| Sessions | `POST /sessions`, `GET /sessions/last`, `PUT /sessions/{id}/notes` |

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
- [tcrypt-lib](https://github.com/RickAtSnowcap/tcrypt-lib) — AES-256-CBC encryption with TPM key delivery

## License

MIT
