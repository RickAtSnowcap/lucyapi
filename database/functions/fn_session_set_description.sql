-- fn_session_set_description.sql
-- Sets (overwrites) the description of an agent's current session — its most recent one, opened by get_context.
-- Returns the updated row; no row if the agent has no session yet.

CREATE OR REPLACE FUNCTION lucyapi.fn_session_set_description(p_agent_id INT, p_description TEXT)
RETURNS TABLE(session_id INT, started_at TIMESTAMPTZ, description TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.sessions s
       SET description = p_description
     WHERE s.session_id = (SELECT x.session_id
                             FROM public.sessions x
                            WHERE x.agent_id = p_agent_id
                            ORDER BY x.started_at DESC, x.session_id DESC
                            LIMIT 1)
    RETURNING s.session_id, s.started_at, s.description;
END;
$proc$;
