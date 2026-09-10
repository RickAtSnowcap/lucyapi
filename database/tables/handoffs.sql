-- handoffs.sql
-- Agent-scoped session handoff prompts.
-- Agents write handoff prompts at end of session for the next session to pick up.
-- Any agent for the same user may read and create (cross-agent delegation).
-- Only the named agent may pickup or delete its own handoffs.

CREATE TABLE IF NOT EXISTS handoffs (
    handoff_id  SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    title       TEXT            NOT NULL,
    prompt      TEXT            NOT NULL,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    picked_up_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_handoffs_agent_id ON handoffs(agent_id);
