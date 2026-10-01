-- 012-session-description.sql
-- set_session_description MCP tool (LucyAPI project #3 §460, 2026-10-01; Rick's design): an agent sets or refreshes its
-- current session's description once the focus is clear and again when wrapping up.
-- sessions.project (free text from the retired create_session) is renamed to description — the old notes carry over.
-- The admin list functions' output column `project` becomes `description`, so they're dropped and re-created.
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction, then deploy the matching binary:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/012-session-description.sql \
--       functions/fn_session_set_description.sql functions/fn_admin_{agent_list,session_list_recent,session_list_by_agent}.sql; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_session_set_description(INTEGER, TEXT) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_agent_list(INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_session_list_recent(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_session_list_by_agent(INTEGER, INTEGER, INTEGER) TO leaddev;'; \
--       echo 'COMMIT;' ) | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

ALTER TABLE public.sessions RENAME COLUMN project TO description;

DROP FUNCTION IF EXISTS lucyapi.fn_admin_agent_list(p_user_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_admin_session_list_recent(p_user_id integer, p_limit integer);
DROP FUNCTION IF EXISTS lucyapi.fn_admin_session_list_by_agent(p_user_id integer, p_agent_id integer, p_limit integer);
