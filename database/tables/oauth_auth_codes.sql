-- oauth_auth_codes.sql
-- Short-lived, single-use authorization codes issued by /oauth/authorize.
-- Only the SHA-256 hash of the code is stored. Each code is bound to the client, the exact
-- redirect_uri, the PKCE S256 challenge, the resource (audience), and ONE agent chosen at consent.

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
