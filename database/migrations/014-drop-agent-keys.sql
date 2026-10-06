-- 014-drop-agent-keys.sql
-- Agent-key retirement phase 2 (LucyAPI project #3 §479, 2026-10-06): agents reach LucyAPI only through the OAuth
-- connector (/mcp/connector), so agent keys are gone. Drops the key lookup function and agents.api_key with its
-- UNIQUE constraint (agents_api_key_key) and index (idx_agents_api_key); the column's values are never read again.
--
-- ORDER MATTERS: deploy the binary without key auth FIRST. The old binary calls fn_agent_get_by_api_key on every
-- key-authenticated request and would fail once this runs.
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/014-drop-agent-keys.sql; echo 'COMMIT;' ) \
--     | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

DROP FUNCTION IF EXISTS lucyapi.fn_agent_get_by_api_key(p_api_key text);

DROP INDEX IF EXISTS public.idx_agents_api_key;
ALTER TABLE public.agents DROP CONSTRAINT IF EXISTS agents_api_key_key;
ALTER TABLE public.agents DROP COLUMN api_key;
