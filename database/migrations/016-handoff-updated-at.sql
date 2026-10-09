-- 016-handoff-updated-at.sql
-- Review follow-up to 015 (handoff #462, 2026-10-09): "pending" isn't "unread". A recipient can read a pending
-- handoff before picking it up, so an edit by the creator must be visible:
--   * handoffs.updated_at: set by fn_handoff_update, NULL until the first edit;
--   * fn_handoff_get / fn_handoff_list_pending / fn_handoff_list_sent return it (return types change: dropped first);
--   * fn_handoff_list_sent now returns up to 101 rows, so the API can say "truncated" when there are more than 100.
-- Not changed (Rick: no-fix): created_by_agent_id has no ON DELETE action. Nothing deletes agents today; any future
-- agent deletion must deal with handoffs it created (and received).
-- Same stop window as 015: the old binary calls the old return types.
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/016-handoff-updated-at.sql \
--       functions/fn_handoff_{get,list_pending,list_sent,update}.sql; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_get(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_list_pending(INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_list_sent(INTEGER, BOOLEAN) TO leaddev;'; \
--       echo 'COMMIT;' ) | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

ALTER TABLE public.handoffs ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;

DROP FUNCTION IF EXISTS lucyapi.fn_handoff_get(p_caller_agent_id integer, p_handoff_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_handoff_list_pending(p_agent_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_handoff_list_sent(p_caller_agent_id integer, p_pending_only boolean);
