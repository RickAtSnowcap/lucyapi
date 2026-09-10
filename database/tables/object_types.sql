-- object_types.sql
-- Lookup table for shareable object types. Seeded separately in seed/object_types.sql.

CREATE TABLE IF NOT EXISTS object_types (
    object_type_id  SMALLINT        PRIMARY KEY,
    name            VARCHAR(50)     NOT NULL UNIQUE
);
