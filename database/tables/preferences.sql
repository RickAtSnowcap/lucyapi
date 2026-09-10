-- preferences.sql
-- Agent-scoped, tree structure. Domain-specific preferences loaded on demand.
-- Any agent belonging to the same user may read; only the named agent may write.

CREATE TABLE IF NOT EXISTS preferences (
    pkid        SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    parent_id   INT             NOT NULL DEFAULT 0,
    title       TEXT            NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_preferences_agent_id  ON preferences(agent_id);
CREATE INDEX IF NOT EXISTS idx_preferences_parent_id ON preferences(agent_id, parent_id);
