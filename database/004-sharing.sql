-- 004-sharing.sql
-- Sharing feature: object_types, shared_objects, and hints.hint_category_id
-- Part 1/3: Database schema only

BEGIN;

-- 1. Create object_types lookup table
CREATE TABLE object_types (
    object_type_id SMALLINT PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE
);

INSERT INTO object_types (object_type_id, name) VALUES
    (1, 'project'),
    (2, 'hint'),
    (3, 'wiki');

-- 2. Create shared_objects table
CREATE TABLE shared_objects (
    share_id SERIAL PRIMARY KEY,
    shared_by_user_id INTEGER NOT NULL REFERENCES users(user_id),
    shared_to_user_id INTEGER NOT NULL REFERENCES users(user_id),
    object_type_id SMALLINT NOT NULL REFERENCES object_types(object_type_id),
    object_id INTEGER NOT NULL,
    permission_level SMALLINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_shared_objects UNIQUE (shared_to_user_id, object_type_id, object_id),
    CONSTRAINT chk_permission_level CHECK (permission_level BETWEEN 1 AND 3),
    CONSTRAINT chk_not_self_share CHECK (shared_by_user_id != shared_to_user_id)
);

CREATE INDEX idx_shared_objects_to_user ON shared_objects (shared_to_user_id, object_type_id);
CREATE INDEX idx_shared_objects_by_user ON shared_objects (shared_by_user_id);

-- 3. Add hint_category_id to hints table
ALTER TABLE hints ADD COLUMN hint_category_id INTEGER;

-- 4. Backfill hint_category_id for existing data
-- Root hints: hint_category_id = own hint_id
UPDATE hints SET hint_category_id = hint_id WHERE parent_id = 0;

-- Child hints: hint_category_id = parent's hint_category_id
UPDATE hints h SET hint_category_id = p.hint_category_id
FROM hints p
WHERE h.parent_id = p.hint_id AND h.hint_category_id IS NULL;

-- Make column NOT NULL after backfill
ALTER TABLE hints ALTER COLUMN hint_category_id SET NOT NULL;

-- Index for permission check join pattern
CREATE INDEX idx_hints_category_id ON hints (hint_category_id);

-- 5. Grant permissions to the lucy database user
GRANT SELECT, INSERT, UPDATE, DELETE ON object_types TO lucy;
GRANT SELECT, INSERT, UPDATE, DELETE ON shared_objects TO lucy;
GRANT USAGE, SELECT ON SEQUENCE shared_objects_share_id_seq TO lucy;

COMMIT;
