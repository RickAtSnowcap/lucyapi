-- project_sections.sql
-- Tree structure under projects. Breaks a project into logical sections with optional file paths.

CREATE TABLE IF NOT EXISTS project_sections (
    section_id  SERIAL          PRIMARY KEY,
    project_id  INT             NOT NULL REFERENCES projects(project_id),
    parent_id   INT             NOT NULL DEFAULT 0,
    title       TEXT            NOT NULL,
    description TEXT,
    file_path   TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_project_sections_project_id ON project_sections(project_id);
CREATE INDEX IF NOT EXISTS idx_project_sections_parent_id  ON project_sections(project_id, parent_id);
