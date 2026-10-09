-- 015-handoff-creator.sql
-- Handoff creators (LucyAPI project #3, 2026-10-09):
--   * handoffs.created_by_agent_id records who sent each handoff (from now on; older rows stay NULL = unknown);
--   * the creator may edit a handoff (fn_handoff_update) and delete it, but only while it is still pending;
--   * the recipient may still delete it at any time; nobody else sees or touches it;
--   * fn_handoff_get also serves the creator, and get/list return to_agent / from_agent names;
--   * new fn_handoff_list_sent: the handoffs an agent created, newest first.
-- fn_handoff_create gains a parameter; fn_handoff_get / list_pending / delete change return types, so all four are
-- dropped first. The old binary calls the old signatures: STOP lucyapi before applying, start it after the new binary
-- is in place (one stop window).
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/015-handoff-creator.sql \
--       functions/fn_handoff_{create,get,list_pending,list_sent,update,delete}.sql; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_create(INTEGER, TEXT, TEXT, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_get(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_list_pending(INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_list_sent(INTEGER, BOOLEAN) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_update(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_delete(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'COMMIT;' ) | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

ALTER TABLE public.handoffs ADD COLUMN IF NOT EXISTS created_by_agent_id INT REFERENCES public.agents(agent_id);
CREATE INDEX IF NOT EXISTS idx_handoffs_created_by ON public.handoffs(created_by_agent_id);

DROP FUNCTION IF EXISTS lucyapi.fn_handoff_create(p_agent_id integer, p_title text, p_prompt text);
DROP FUNCTION IF EXISTS lucyapi.fn_handoff_get(p_agent_id integer, p_handoff_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_handoff_list_pending(p_agent_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_handoff_delete(p_agent_id integer, p_handoff_id integer);
