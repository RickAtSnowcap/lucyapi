-- 006-oauth.sql
-- LucyAPI OAuth (project #62, Phase 2): tables for the OAuth 2.1 authorization server that
-- fronts the MCP connector endpoint. Schema only — functions live in functions/fn_oauth_*.sql.
--
-- Apply (objects owned by leaddev — see hint #189):
--   ( echo 'SET ROLE leaddev;'; cat migrations/006-oauth.sql tables/oauth_*.sql functions/fn_oauth_*.sql ) \
--     | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1
-- (tables/oauth_*.sql are CREATE ... IF NOT EXISTS, so re-running is harmless.)

BEGIN;

CREATE TABLE IF NOT EXISTS oauth_clients (
    client_id           TEXT            PRIMARY KEY,
    registration_type   TEXT            NOT NULL CHECK (registration_type IN ('dcr', 'cimd')),
    client_name         TEXT            NULL,
    redirect_uris       TEXT[]          NOT NULL,
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    metadata_fetched_at TIMESTAMPTZ     NULL,
    last_used_at        TIMESTAMPTZ     NULL
);

CREATE TABLE IF NOT EXISTS oauth_auth_codes (
    code_hash       TEXT            PRIMARY KEY,
    client_id       TEXT            NOT NULL REFERENCES oauth_clients(client_id) ON DELETE CASCADE,
    redirect_uri    TEXT            NOT NULL,
    code_challenge  TEXT            NOT NULL,
    resource        TEXT            NOT NULL,
    scope           TEXT            NULL,
    user_id         INT             NOT NULL REFERENCES users(user_id),
    agent_id        INT             NOT NULL REFERENCES agents(agent_id) ON DELETE CASCADE,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ     NOT NULL,
    used_at         TIMESTAMPTZ     NULL
);
CREATE INDEX IF NOT EXISTS idx_oauth_auth_codes_expires ON oauth_auth_codes(expires_at);

CREATE TABLE IF NOT EXISTS oauth_tokens (
    token_id        BIGSERIAL       PRIMARY KEY,
    token_hash      TEXT            NOT NULL UNIQUE,
    kind            TEXT            NOT NULL CHECK (kind IN ('access', 'refresh')),
    family_id       UUID            NOT NULL,
    client_id       TEXT            NOT NULL REFERENCES oauth_clients(client_id) ON DELETE CASCADE,
    user_id         INT             NOT NULL REFERENCES users(user_id),
    agent_id        INT             NOT NULL REFERENCES agents(agent_id) ON DELETE CASCADE,
    resource        TEXT            NOT NULL,
    scope           TEXT            NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ     NOT NULL,
    rotated_at      TIMESTAMPTZ     NULL,
    revoked_at      TIMESTAMPTZ     NULL,
    last_used_at    TIMESTAMPTZ     NULL
);
CREATE INDEX IF NOT EXISTS idx_oauth_tokens_family   ON oauth_tokens(family_id);
CREATE INDEX IF NOT EXISTS idx_oauth_tokens_agent    ON oauth_tokens(agent_id);
CREATE INDEX IF NOT EXISTS idx_oauth_tokens_expires  ON oauth_tokens(expires_at);

COMMIT;
