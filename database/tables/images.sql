-- images.sql
-- User-scoped image metadata. Tracks AI-generated and uploaded images.
-- Actual files stored on disk; this table holds metadata only.

CREATE TABLE IF NOT EXISTS images (
    image_id    SERIAL          PRIMARY KEY,
    filename    TEXT            NOT NULL,
    prompt      TEXT,
    model       TEXT,
    created_at  TIMESTAMPTZ     DEFAULT NOW(),
    keep        BOOLEAN         DEFAULT FALSE,
    size_bytes  INT,
    width       INT,
    height      INT,
    user_id     INT             REFERENCES users(user_id)
);

CREATE INDEX IF NOT EXISTS idx_images_created ON images(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_images_keep    ON images(keep);
