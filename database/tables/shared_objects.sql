-- shared_objects.sql
-- Cross-user sharing of projects, hints, and wikis.
-- permission_level: 1=read-only, 2=read-write, 3=admin.

CREATE TABLE IF NOT EXISTS shared_objects (
    share_id            SERIAL      PRIMARY KEY,
    shared_by_user_id   INT         NOT NULL REFERENCES users(user_id),
    shared_to_user_id   INT         NOT NULL REFERENCES users(user_id),
    object_type_id      SMALLINT    NOT NULL REFERENCES object_types(object_type_id),
    object_id           INT         NOT NULL,
    permission_level    SMALLINT    NOT NULL DEFAULT 1,
    CONSTRAINT uq_shared_objects     UNIQUE (shared_to_user_id, object_type_id, object_id),
    CONSTRAINT chk_permission_level  CHECK (permission_level >= 1 AND permission_level <= 3),
    CONSTRAINT chk_not_self_share    CHECK (shared_by_user_id <> shared_to_user_id)
);

CREATE INDEX IF NOT EXISTS idx_shared_objects_to_user ON shared_objects(shared_to_user_id, object_type_id);
CREATE INDEX IF NOT EXISTS idx_shared_objects_by_user ON shared_objects(shared_by_user_id);
