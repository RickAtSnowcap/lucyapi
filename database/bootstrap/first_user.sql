-- bootstrap/first_user.sql
-- Creates a login user and that user's first agent, so a freshly built database can be signed in to
-- (LucyAdmin, and the OAuth consent screen where you pick the agent). Run it after rebuild.sql.
-- There is no API for creating users: this script is how the first one gets in.
--
-- Usage (see README.md):
--   psql -d lucyapi -v ON_ERROR_STOP=1 -v user_name='Ada Lovelace' -v username=ada -v agent_name=ada-agent \
--        [-v email=ada@example.com] -f database/bootstrap/first_user.sql
-- The password hash comes from tools/hash_password.py; the script prompts for it, so it never lands in your
-- shell history or the process list.

\if :{?user_name}
\else
    DO $$ BEGIN RAISE EXCEPTION 'Set -v user_name=... (display name)'; END $$;
    \quit
\endif
\if :{?username}
\else
    DO $$ BEGIN RAISE EXCEPTION 'Set -v username=... (login name)'; END $$;
    \quit
\endif
\if :{?agent_name}
\else
    DO $$ BEGIN RAISE EXCEPTION 'Set -v agent_name=... (the agent picked on the OAuth consent screen)'; END $$;
    \quit
\endif
\if :{?email}
\else
    \set email ''
\endif
\if :{?password_hash}
\else
    \prompt 'Password hash (from tools/hash_password.py): ' password_hash
\endif

BEGIN;

INSERT INTO users (name, username, password_hash, email)
VALUES (:'user_name', :'username', :'password_hash', NULLIF(:'email', ''))
RETURNING user_id AS new_user_id \gset

INSERT INTO agents (user_id, name)
VALUES (:new_user_id, :'agent_name');

COMMIT;

\echo 'Created user' :'username' 'with user_id' :new_user_id 'and agent' :'agent_name'
