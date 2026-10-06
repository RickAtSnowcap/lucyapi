-- agents.sql
-- Each agent belongs to one user. Agents authenticate with OAuth bearer tokens (oauth_tokens), not stored keys.

CREATE TABLE IF NOT EXISTS agents (
    agent_id    SERIAL          PRIMARY KEY,
    user_id     INT             NOT NULL REFERENCES users(user_id),
    name        TEXT            NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_agents_user_id  ON agents(user_id);
