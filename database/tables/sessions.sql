-- sessions.sql
-- Agent-scoped. One row per agent session: every get_context call opens one (fn_context_get_full).
-- Used for gap detection ("previous session") and, with session_projects, what each session worked on.
-- description: set by the agent via set_session_description (overwritten on each call). Pre-2026-10-01 rows
-- hold the free-text note from the retired create_session tool (this column was named `project`).

CREATE TABLE IF NOT EXISTS sessions (
    session_id  SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    started_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    description TEXT
);

CREATE INDEX IF NOT EXISTS idx_sessions_agent_id ON sessions(agent_id, started_at DESC);
