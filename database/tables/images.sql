-- images.sql
-- User-scoped image metadata. Tracks AI-generated, edited and uploaded images.
-- Actual files stored on disk; this table holds metadata only.
-- Images are public by design: anyone with the /nanoimages/ URL can view them.

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
    user_id     INT             REFERENCES users(user_id),
    title       TEXT,
    description TEXT,
    mime_type   TEXT,
    source      TEXT            CHECK (source IN ('generated', 'edited', 'uploaded')),
    agent_id    INT             REFERENCES agents(agent_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_images_created ON images(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_images_keep    ON images(keep);
