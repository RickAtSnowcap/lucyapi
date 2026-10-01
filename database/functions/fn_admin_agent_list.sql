-- fn_admin_agent_list.sql
-- The caller's agents (admin Agents page), each with its most recent session, the projects loaded in it,
-- and last_used_at: the latest OAuth connector use across the agent's tokens (updated at most once a minute).

CREATE OR REPLACE FUNCTION lucyapi.fn_admin_agent_list(p_user_id INT)
RETURNS TABLE(agent_id INT, name TEXT, session_id INT, started_at TIMESTAMPTZ, project TEXT,
              project_ids INT[], project_titles TEXT[], last_used_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT a.agent_id, a.name, s.session_id, s.started_at, s.project,
           COALESCE(sp.ids, '{}'), COALESCE(sp.titles, '{}'),
           (SELECT max(t.last_used_at) FROM public.oauth_tokens t WHERE t.agent_id = a.agent_id)
      FROM public.agents a
      LEFT JOIN LATERAL (
          SELECT se.session_id, se.started_at, se.project
            FROM public.sessions se
           WHERE se.agent_id = a.agent_id
           ORDER BY se.started_at DESC, se.session_id DESC
           LIMIT 1
      ) s ON TRUE
      LEFT JOIN LATERAL (
          SELECT array_agg(p.project_id ORDER BY x.first_loaded_at) AS ids,
                 array_agg(p.title ORDER BY x.first_loaded_at) AS titles
            FROM public.session_projects x
            JOIN public.projects p ON p.project_id = x.project_id
           WHERE x.session_id = s.session_id
      ) sp ON TRUE
     WHERE a.user_id = p_user_id
     ORDER BY a.name;
END;
$proc$;
