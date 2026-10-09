-- handoffs.sql
-- Agent-scoped handoff prompts: work sent to an agent (its own next session, or another agent of the same user).
-- created_by_agent_id is the sender (NULL for handoffs created before migration 015: sender unknown).
-- The recipient (agent_id) may get, pick up and delete it at any time.
-- The creator may get it, and edit or delete it only while it is pending (picked_up_at IS NULL).
-- updated_at records the creator's last edit, so a recipient that already read it can tell it changed.

CREATE TABLE IF NOT EXISTS handoffs (
    handoff_id  SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    title       TEXT            NOT NULL,
    prompt      TEXT            NOT NULL,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    picked_up_at TIMESTAMPTZ,
    created_by_agent_id INT     REFERENCES agents(agent_id),
    updated_at  TIMESTAMPTZ     -- last edit by the creator (NULL = never edited)
);

CREATE INDEX IF NOT EXISTS idx_handoffs_agent_id ON handoffs(agent_id);
CREATE INDEX IF NOT EXISTS idx_handoffs_created_by ON handoffs(created_by_agent_id);
