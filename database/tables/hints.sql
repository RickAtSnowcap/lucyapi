-- hints.sql
-- User-scoped, tree structure. Reusable guidance organized by category.
-- hint_category_id links child hints to their root category for permission checks.

CREATE TABLE IF NOT EXISTS hints (
    hint_id         SERIAL          PRIMARY KEY,
    user_id         INT             NOT NULL REFERENCES users(user_id),
    parent_id       INT             NOT NULL DEFAULT 0,
    title           VARCHAR(255)    NOT NULL,
    description     TEXT,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    hint_category_id INT            NOT NULL,
    sort_order       INT            NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_hints_user_id      ON hints(user_id);
CREATE INDEX IF NOT EXISTS idx_hints_parent_id    ON hints(parent_id);
CREATE INDEX IF NOT EXISTS idx_hints_category_id  ON hints(hint_category_id);
