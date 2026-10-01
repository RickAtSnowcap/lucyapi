-- sessions.sql
-- Agent-scoped. One row per agent session: every get_context call opens one (fn_context_get_full).
-- Used for gap detection ("previous session") and, with session_projects, what each session worked on.
-- project: free-text note from the retired create_session tool (pre-2026-10-01 rows only).

CREATE TABLE IF NOT EXISTS sessions (
    session_id  SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    started_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    project     TEXT
);

CREATE INDEX IF NOT EXISTS idx_sessions_agent_id ON sessions(agent_id, started_at DESC);
