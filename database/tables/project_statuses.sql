-- project_statuses.sql
-- Lookup table for project workflow states. Seeded separately in seed/project_statuses.sql.

CREATE TABLE IF NOT EXISTS project_statuses (
    status_id   SERIAL          PRIMARY KEY,
    code        VARCHAR(20)     NOT NULL UNIQUE,
    label       VARCHAR(50)     NOT NULL,
    sort_order  INT             NOT NULL
);
