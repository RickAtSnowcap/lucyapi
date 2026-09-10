-- fn_session_create.sql
-- Records a new session start for an agent. p_project may be NULL.

CREATE OR REPLACE FUNCTION lucyapi.fn_session_create(p_agent_id INT, p_project TEXT)
RETURNS TABLE(session_id INT, started_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.sessions (agent_id, project)
    VALUES (p_agent_id, p_project)
    RETURNING sessions.session_id, sessions.started_at;
END;
$proc$;
