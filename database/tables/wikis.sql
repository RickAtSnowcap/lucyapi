-- wikis.sql
-- User-scoped, shared across all user's agents. Knowledge base containers.

CREATE TABLE IF NOT EXISTS wikis (
    wiki_id     SERIAL          PRIMARY KEY,
    user_id     INT             NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    title       VARCHAR(500)    NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_wikis_user_id ON wikis(user_id);
