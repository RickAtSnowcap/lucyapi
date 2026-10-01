-- fn_session_add_project.sql
-- Records that an agent loaded a project in its current (most recent) session. Called after a successful
-- get_project / get_project_compact. Loading the same project again is a no-op; no session yet = no-op.
-- The caller has already passed the project access check (it just loaded the project).

CREATE OR REPLACE FUNCTION lucyapi.fn_session_add_project(p_agent_id INT, p_project_id INT)
RETURNS VOID
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    INSERT INTO public.session_projects (session_id, project_id)
    SELECT s.session_id, p_project_id
      FROM public.sessions s
     WHERE s.agent_id = p_agent_id
     ORDER BY s.started_at DESC, s.session_id DESC
     LIMIT 1
    ON CONFLICT (session_id, project_id) DO NOTHING;
END;
$proc$;
