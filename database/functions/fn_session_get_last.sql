-- fn_session_get_last.sql
-- Returns the most recent session for an agent.

CREATE OR REPLACE FUNCTION lucyapi.fn_session_get_last(p_agent_id INT)
RETURNS TABLE(session_id INT, started_at TIMESTAMPTZ, project TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT s.session_id,
           s.started_at,
           s.project
      FROM public.sessions s
     WHERE s.agent_id = p_agent_id
     ORDER BY s.started_at DESC
     LIMIT 1;
END;
$proc$;
