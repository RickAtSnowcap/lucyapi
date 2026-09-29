-- oauth_tokens.sql
-- Access and refresh tokens for the MCP connector. Opaque random strings; only their SHA-256
-- hashes are stored. Every token is bound to one agent, one client, and the resource (audience).
--
-- family_id groups a login's chain of rotated refresh tokens (and their access tokens).
-- Refresh tokens rotate on every use; presenting an already-rotated refresh token is treated
-- as theft and revokes the whole family.

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
    rotated_at      TIMESTAMPTZ     NULL,   -- refresh only: exchanged for a new pair
    revoked_at      TIMESTAMPTZ     NULL,
    last_used_at    TIMESTAMPTZ     NULL
);

CREATE INDEX IF NOT EXISTS idx_oauth_tokens_family   ON oauth_tokens(family_id);
CREATE INDEX IF NOT EXISTS idx_oauth_tokens_agent    ON oauth_tokens(agent_id);
CREATE INDEX IF NOT EXISTS idx_oauth_tokens_expires  ON oauth_tokens(expires_at);
