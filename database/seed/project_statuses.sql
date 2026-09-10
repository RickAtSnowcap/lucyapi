-- seed/project_statuses.sql
-- Lookup data for project workflow states.

INSERT INTO project_statuses (status_id, code, label, sort_order) VALUES
    (1, 'planning',  'Planning',  1),
    (2, 'active',    'Active',    2),
    (3, 'on_hold',   'On Hold',   3),
    (4, 'evolving',  'Evolving',  4),
    (5, 'complete',  'Complete',  5),
    (6, 'archived',  'Archived',  6)
ON CONFLICT (status_id) DO UPDATE
    SET code       = EXCLUDED.code,
        label      = EXCLUDED.label,
        sort_order = EXCLUDED.sort_order;
