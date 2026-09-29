-- fn_project_update.sql
-- Updates a project using COALESCE to preserve fields not provided. Sets updated_at = NOW().
-- Requires edit access (fn_access_level >= 2); otherwise returns no row (treated as not found).

CREATE OR REPLACE FUNCTION lucyapi.fn_project_update(p_user_id INT, p_project_id INT, p_title TEXT, p_description TEXT, p_status_id INT)
RETURNS TABLE(project_id INT, title TEXT, status TEXT, status_label TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 2 THEN
        RETURN;
    END IF;

    UPDATE public.projects p
       SET title       = COALESCE(p_title, p.title),
           description = COALESCE(p_description, p.description),
           status_id   = COALESCE(p_status_id, p.status_id),
           updated_at  = NOW()
     WHERE p.project_id = p_project_id;

    RETURN QUERY
    SELECT p.project_id,
           p.title,
           ps.code::TEXT  AS status,
           ps.label::TEXT AS status_label
      FROM public.projects p
      JOIN public.project_statuses ps ON ps.status_id = p.status_id
     WHERE p.project_id = p_project_id;
END;
$proc$;
