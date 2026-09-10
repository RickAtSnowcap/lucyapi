-- wiki_section_tags.sql
-- Tags on wiki sections for categorization and search.

CREATE TABLE IF NOT EXISTS wiki_section_tags (
    tag_id      SERIAL          PRIMARY KEY,
    section_id  INT             NOT NULL REFERENCES wiki_sections(section_id) ON DELETE CASCADE,
    tag         VARCHAR(100)    NOT NULL,
    UNIQUE(section_id, tag)
);

CREATE INDEX IF NOT EXISTS idx_wiki_section_tags_tag ON wiki_section_tags(tag);
