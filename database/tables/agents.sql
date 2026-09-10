-- agents.sql
-- Each agent belongs to one user and carries its own API key for authentication.

CREATE TABLE IF NOT EXISTS agents (
    agent_id    SERIAL          PRIMARY KEY,
    user_id     INT             NOT NULL REFERENCES users(user_id),
    name        TEXT            NOT NULL UNIQUE,
    api_key     TEXT            NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_agents_user_id  ON agents(user_id);
CREATE INDEX IF NOT EXISTS idx_agents_api_key  ON agents(api_key);
