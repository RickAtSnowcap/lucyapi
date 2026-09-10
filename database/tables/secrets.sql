-- secrets.sql
-- User-scoped, encrypted key-value storage.
-- Stores credentials and sensitive values encrypted at rest via AES-256-GCM.
-- Any agent belonging to the user may read and write.

CREATE TABLE IF NOT EXISTS secrets (
    secret_id       SERIAL          PRIMARY KEY,
    user_id         INT             NOT NULL REFERENCES users(user_id),
    key             TEXT            NOT NULL,
    encrypted_value BYTEA           NOT NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_secrets_user_key UNIQUE (user_id, key)
);

CREATE INDEX IF NOT EXISTS idx_secrets_user_id ON secrets(user_id);
