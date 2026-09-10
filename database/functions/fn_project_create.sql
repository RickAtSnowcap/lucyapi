-- fn_project_create.sql
-- Inserts a new project and returns the created row with status info.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_create(p_user_id INT, p_title TEXT, p_description TEXT, p_status_id INT)
RETURNS TABLE(project_id INT, title TEXT, status TEXT, status_label TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_project_id INT;
BEGIN
    INSERT INTO public.projects (user_id, title, description, status_id)
    VALUES (p_user_id, p_title, p_description, COALESCE(p_status_id, 1))
    RETURNING projects.project_id INTO v_project_id;

    RETURN QUERY
    SELECT p.project_id,
           p.title,
           ps.code::TEXT  AS status,
           ps.label::TEXT AS status_label
      FROM public.projects p
      JOIN public.project_statuses ps ON ps.status_id = p.status_id
     WHERE p.project_id = v_project_id;
END;
$proc$;
