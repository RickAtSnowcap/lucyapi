-- projects.sql
-- User-scoped, shared across all user's agents. Tracks initiatives and work streams.

CREATE TABLE IF NOT EXISTS projects (
    project_id  SERIAL          PRIMARY KEY,
    user_id     INT             NOT NULL REFERENCES users(user_id),
    title       TEXT            NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    status_id   INT             NOT NULL DEFAULT 1 REFERENCES project_statuses(status_id)
);

CREATE INDEX IF NOT EXISTS idx_projects_user_id   ON projects(user_id);
CREATE INDEX IF NOT EXISTS idx_projects_status_id ON projects(status_id);
