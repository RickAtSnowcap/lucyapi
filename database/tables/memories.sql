-- memories.sql
-- Agent-scoped, flat structure. Personal details, stories, relationships.
-- Titles always-load for ambient recall.
-- Agent may add freely. User must approve changes and deletions.

CREATE TABLE IF NOT EXISTS memories (
    pkid        SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    title       TEXT            NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_memories_agent_id ON memories(agent_id);
