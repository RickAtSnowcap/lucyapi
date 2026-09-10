-- always_load.sql
-- Agent-scoped, tree structure. Core identity and behavioral context that loads every session.
-- Any agent belonging to the same user may read; only the named agent may write.

CREATE TABLE IF NOT EXISTS always_load (
    pkid        SERIAL          PRIMARY KEY,
    agent_id    INT             NOT NULL REFERENCES agents(agent_id),
    parent_id   INT             NOT NULL DEFAULT 0,
    title       TEXT            NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_always_load_agent_id  ON always_load(agent_id);
CREATE INDEX IF NOT EXISTS idx_always_load_parent_id ON always_load(agent_id, parent_id);
