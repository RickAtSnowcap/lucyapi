-- fn_admin_agent_list.sql
-- The caller's agents, each with its most recent session (admin Agents page).

CREATE OR REPLACE FUNCTION lucyapi.fn_admin_agent_list(p_user_id INT)
RETURNS TABLE(agent_id INT, name TEXT, session_id INT, started_at TIMESTAMPTZ, project TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT a.agent_id, a.name, s.session_id, s.started_at, s.project
      FROM public.agents a
      LEFT JOIN LATERAL (
          SELECT se.session_id, se.started_at, se.project
            FROM public.sessions se
           WHERE se.agent_id = a.agent_id
           ORDER BY se.started_at DESC
           LIMIT 1
      ) s ON TRUE
     WHERE a.user_id = p_user_id
     ORDER BY a.name;
END;
$proc$;
