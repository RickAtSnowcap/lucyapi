-- sessions.sql
-- Agent-scoped. Tracks conversation starts for gap detection.

CREATE TABLE IF NOT EXISTS sessions (
    session_id  SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    started_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    project     TEXT
);

CREATE INDEX IF NOT EXISTS idx_sessions_agent_id ON sessions(agent_id, started_at DESC);
