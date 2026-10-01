-- 011-session-tracking.sql
-- Automatic session tracking (LucyAPI project #3, 2026-10-01; Rick's design):
--   * every get_context call opens a session (fn_context_get_full) and returns it, with the previous session's start;
--   * get_project / get_project_compact attach the loaded project to the agent's current session
--     (new table session_projects, fn_session_add_project) — the session's work focus, recorded invisibly;
--   * create_session / get_last_session are retired (fn_session_create, fn_session_get_last dropped);
--   * admin lists return each session's projects; the agent list adds last_used_at (OAuth connector);
--     new fn_admin_session_list_by_agent for the agent detail Sessions tab.
-- fn_admin_agent_list / fn_admin_session_list_recent change return types, so they're dropped first.
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction, then deploy the matching binary:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/011-session-tracking.sql tables/session_projects.sql \
--       functions/fn_session_add_project.sql functions/fn_context_get_full.sql \
--       functions/fn_admin_{agent_list,session_list_recent,session_list_by_agent}.sql; \
--       echo 'GRANT SELECT, INSERT, UPDATE, DELETE ON session_projects TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_session_add_project(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_agent_list(INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_session_list_recent(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_session_list_by_agent(INTEGER, INTEGER, INTEGER) TO leaddev;'; \
--       echo 'COMMIT;' ) | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

DROP FUNCTION IF EXISTS lucyapi.fn_admin_agent_list(p_user_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_admin_session_list_recent(p_user_id integer, p_limit integer);
DROP FUNCTION IF EXISTS lucyapi.fn_session_create(p_agent_id integer, p_project text);
DROP FUNCTION IF EXISTS lucyapi.fn_session_get_last(p_agent_id integer);
