-- wiki_sections.sql
-- Tree structure under wikis. Each section holds a title and markdown description.

CREATE TABLE IF NOT EXISTS wiki_sections (
    section_id  SERIAL          PRIMARY KEY,
    wiki_id     INT             NOT NULL REFERENCES wikis(wiki_id) ON DELETE CASCADE,
    parent_id   INT             NOT NULL DEFAULT 0,
    title       VARCHAR(500)    NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_wiki_sections_wiki_id   ON wiki_sections(wiki_id);
CREATE INDEX IF NOT EXISTS idx_wiki_sections_parent_id ON wiki_sections(wiki_id, parent_id);
