-- users.sql
-- Core identity table. Every agent, project, wiki, hint, secret, and image belongs to a user.

CREATE TABLE IF NOT EXISTS users (
    user_id     SERIAL          PRIMARY KEY,
    name        TEXT            NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    username    TEXT            NOT NULL UNIQUE,
    password_hash TEXT,
    email       TEXT
);
