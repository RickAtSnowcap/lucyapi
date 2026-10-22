# LucyAPI database

Everything needed to build an empty, working LucyAPI database on a fresh PostgreSQL server: schema, tables,
indexes, functions, reference data and grants. No real data lives here.

Tested on PostgreSQL 16. No extensions are needed.

## Layout

| Path | What it is |
|------|------------|
| `rebuild.sql` | Master script. Builds every object in dependency order, then seeds and grants. |
| `tables/` | One file per table (current definition, including columns added by later migrations). |
| `functions/` | One file per `lucyapi.fn_*` function (current signature and body). |
| `seed/` | Reference data only: `project_statuses`, `object_types`. |
| `grants.sql` | Privileges for the application role (run by `rebuild.sql`). |
| `bootstrap/first_user.sql` | Creates a login user and their first agent. |
| `migrations/` | History: the changes applied to databases built earlier. A fresh build does not use them. |

Tables live in `public`. Functions live in the `lucyapi` schema.

## Build a fresh database

Run these as a PostgreSQL superuser (or any role that can create roles and databases). Two roles keep ownership
apart from the application:
- `lucyapi_owner` owns every object. It has no login; you run builds and migrations as a member of it.
- `lucyapi_app` is the login LucyAPI connects as. It gets only read/write on tables and `EXECUTE` on functions.

The names are the defaults; pass `-v owner_role=...` / `-v app_role=...` to use others.

**1. Create the roles.**

```sql
CREATE ROLE lucyapi_owner NOLOGIN;
CREATE ROLE lucyapi_app LOGIN PASSWORD '<choose one>';
```

**2. Create the database**, owned by the owner role (so it can create the schema and tables in `public`).

```bash
createdb -O lucyapi_owner lucyapi
```

**3. Build the schema.** `rebuild.sql` uses `\ir`, so you can run it from any directory.

```bash
psql -d lucyapi -v ON_ERROR_STOP=1 -v owner_role=lucyapi_owner -f database/rebuild.sql
```

**4. Create the first user and agent.** No API creates users, so this is how the first one gets in. Hash the
password first (hidden prompt; it prints only the hash):

```bash
python3 tools/hash_password.py
```

Then run the bootstrap script. It prompts for the hash, so the hash stays out of your shell history and the process list:

```bash
psql -d lucyapi -v ON_ERROR_STOP=1 \
     -v user_name='Ada Lovelace' -v username=ada -v agent_name=ada-agent \
     -f database/bootstrap/first_user.sql
```

`username` and the password sign in to LucyAdmin (`/auth/login`) and to the OAuth consent screen. On that screen
you pick `agent_name`. Run the script again to add more users. Add more agents for an existing user with
`INSERT INTO agents (user_id, name) VALUES (...)`.

**5. Point LucyAPI at it.** Production reads the connection string from the sealed `Suitcase:DbConnection`.
Development reads `LUCYAPI_CONNECTION_STRING` (see the main README).

## Re-running on an existing database

`rebuild.sql` is safe to re-run:
- tables and indexes use `IF NOT EXISTS`;
- functions use `CREATE OR REPLACE`;
- seed data upserts;
- grants are idempotent.

It does **not** alter existing tables, change a function's return type, or drop removed objects. Those changes
are what `migrations/` are for.

## Changing the schema

Run migrations as the owner role (`SET ROLE lucyapi_owner;` first), so new objects get the right owner.

A change lands in four places:
1. a new numbered file in `migrations/`, for existing databases;
2. the matching file in `tables/` or `functions/`, so a fresh build gets the current shape;
3. `rebuild.sql`, for any new file;
4. `grants.sql`, for new tables, sequences or function signatures (use the exact argument types).

## Differences from Snowcap's live instance

These are deliberate. They were checked on 2026-10-22 by building from this folder and diffing it against the
live database (schema, grants and reference data):
- **Extensions.** The live database also has `pg_trgm` and `fuzzystrmatch`. LucyAPI doesn't use either.
- **Comments.** A few live function bodies are missing the explanatory comments that the files here have.
  The code is the same.
