-- nudges.sql
-- User-scoped deadline/reminder system. Agents call fn_nudge_get_actionable
-- via get_context; the function returns only nudges worth mentioning and
-- auto-stamps last_reminded so agents don't spam.

CREATE TABLE IF NOT EXISTS nudges (
    nudge_id      SERIAL          PRIMARY KEY,
    user_id       INT             NOT NULL REFERENCES users(user_id),
    title         TEXT            NOT NULL,
    description   TEXT,
    due_date      DATE,
    last_reminded TIMESTAMPTZ,
    created_at    TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_nudges_user_id ON nudges(user_id);
