-- fn_project_get_compact.sql
-- Returns a single project header with title and status only (no description).
-- Same permission check as fn_project_get: ownership first, then shared access.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_get_compact(p_project_id INT, p_user_id INT)
RETURNS TABLE(project_id INT, title TEXT, status TEXT, status_label TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    -- Check ownership first
    RETURN QUERY
    SELECT p.project_id,
           p.title,
           ps.code::TEXT   AS status,
           ps.label::TEXT  AS status_label
      FROM public.projects p
      JOIN public.project_statuses ps ON ps.status_id = p.status_id
     WHERE p.project_id = p_project_id
       AND p.user_id = p_user_id;

    IF FOUND THEN
        RETURN;
    END IF;

    -- Check shared access
    RETURN QUERY
    SELECT p.project_id,
           p.title,
           ps.code::TEXT   AS status,
           ps.label::TEXT  AS status_label
      FROM public.shared_objects so
      JOIN public.projects p ON p.project_id = so.object_id
      JOIN public.project_statuses ps ON ps.status_id = p.status_id
     WHERE so.object_type_id = 1
       AND so.object_id = p_project_id
       AND so.shared_to_user_id = p_user_id
       AND so.permission_level >= 1;
END;
$proc$;
