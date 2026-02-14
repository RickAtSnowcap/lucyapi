-- ============================================================
-- Wiki Tables
-- Database: lucyapi (on Lito)
-- Created: February 14, 2026
-- ============================================================

-- Wikis (user-scoped, shared across all user's agents)
CREATE TABLE wikis (
    wiki_id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    title VARCHAR(500) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_wikis_user_id ON wikis(user_id);

-- Wiki Sections (tree structure under wikis)
CREATE TABLE wiki_sections (
    section_id SERIAL PRIMARY KEY,
    wiki_id INTEGER NOT NULL REFERENCES wikis(wiki_id) ON DELETE CASCADE,
    parent_id INTEGER NOT NULL DEFAULT 0,
    title VARCHAR(500) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_wiki_sections_wiki_id ON wiki_sections(wiki_id);
CREATE INDEX idx_wiki_sections_parent_id ON wiki_sections(wiki_id, parent_id);

-- Wiki Section Tags
CREATE TABLE wiki_section_tags (
    tag_id SERIAL PRIMARY KEY,
    section_id INTEGER NOT NULL REFERENCES wiki_sections(section_id) ON DELETE CASCADE,
    tag VARCHAR(100) NOT NULL,
    UNIQUE(section_id, tag)
);

CREATE INDEX idx_wiki_section_tags_tag ON wiki_section_tags(tag);

-- Grants for lucy application user
GRANT SELECT, INSERT, UPDATE, DELETE ON wikis, wiki_sections, wiki_section_tags TO lucy;
GRANT USAGE, SELECT ON SEQUENCE wikis_wiki_id_seq, wiki_sections_section_id_seq, wiki_section_tags_tag_id_seq TO lucy;
