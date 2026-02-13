-- ============================================================
-- LucyAPI Migration 002: Secrets + Handoffs
-- Database: lucyapi (on Lito)
-- Created: February 11, 2026
-- ============================================================

-- Secrets (user-scoped, encrypted key-value storage)
-- Stores credentials and sensitive values encrypted at rest via AES-256-GCM.
-- Any agent belonging to the user may read and write.
CREATE TABLE secrets (
    secret_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id),
    key TEXT NOT NULL,
    encrypted_value BYTEA NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_secrets_user_key UNIQUE (user_id, key)
);

CREATE INDEX idx_secrets_user_id ON secrets(user_id);

-- Handoffs (agent-scoped session handoff prompts)
-- Agents write handoff prompts at end of session for the next session to pick up.
-- Any agent for the same user may read and create (cross-agent delegation).
-- Only the named agent may pickup or delete its own handoffs.
CREATE TABLE handoffs (
    handoff_id SERIAL PRIMARY KEY,
    agent_id INT NOT NULL REFERENCES agents(agent_id),
    title TEXT NOT NULL,
    prompt TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    picked_up_at TIMESTAMPTZ
);

CREATE INDEX idx_handoffs_agent_id ON handoffs(agent_id);
