-- fn_admin_session_list_recent.sql
-- The caller's most recent sessions across all their agents (admin dashboard).

CREATE OR REPLACE FUNCTION lucyapi.fn_admin_session_list_recent(p_user_id INT, p_limit INT)
RETURNS TABLE(session_id INT, agent_name TEXT, started_at TIMESTAMPTZ, project TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT s.session_id, a.name, s.started_at, s.project
      FROM public.sessions s
      JOIN public.agents a ON a.agent_id = s.agent_id
     WHERE a.user_id = p_user_id
     ORDER BY s.started_at DESC
     LIMIT p_limit;
END;
$proc$;
