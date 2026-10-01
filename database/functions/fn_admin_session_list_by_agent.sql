-- fn_admin_session_list_by_agent.sql
-- One agent's sessions, newest first, with the projects loaded in each (admin agent detail → Sessions tab).
-- Only the caller's own agents; anything else returns no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_admin_session_list_by_agent(p_user_id INT, p_agent_id INT, p_limit INT)
RETURNS TABLE(session_id INT, started_at TIMESTAMPTZ, description TEXT, project_ids INT[], project_titles TEXT[])
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT s.session_id, s.started_at, s.description,
           COALESCE(sp.ids, '{}'), COALESCE(sp.titles, '{}')
      FROM public.sessions s
      JOIN public.agents a ON a.agent_id = s.agent_id
      LEFT JOIN LATERAL (
          SELECT array_agg(p.project_id ORDER BY x.first_loaded_at) AS ids,
                 array_agg(p.title ORDER BY x.first_loaded_at) AS titles
            FROM public.session_projects x
            JOIN public.projects p ON p.project_id = x.project_id
           WHERE x.session_id = s.session_id
      ) sp ON TRUE
     WHERE s.agent_id = p_agent_id
       AND a.user_id = p_user_id
     ORDER BY s.started_at DESC, s.session_id DESC
     LIMIT p_limit;
END;
$proc$;
