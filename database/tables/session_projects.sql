-- session_projects.sql
-- Projects an agent loaded (get_project / get_project_compact) during a session — the session's work focus.
-- Filled automatically by fn_session_add_project; one row per project per session.

CREATE TABLE IF NOT EXISTS session_projects (
    session_id      INT             NOT NULL REFERENCES sessions(session_id) ON DELETE CASCADE,
    project_id      INT             NOT NULL REFERENCES projects(project_id) ON DELETE CASCADE,
    first_loaded_at TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    PRIMARY KEY (session_id, project_id)
);

CREATE INDEX IF NOT EXISTS idx_session_projects_project ON session_projects(project_id);
