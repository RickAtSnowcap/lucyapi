-- seed/object_types.sql
-- Lookup data for shareable object types.

INSERT INTO object_types (object_type_id, name) VALUES
    (1, 'project'),
    (2, 'hint'),
    (3, 'wiki')
ON CONFLICT (object_type_id) DO UPDATE
    SET name = EXCLUDED.name;
