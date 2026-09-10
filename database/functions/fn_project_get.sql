-- fn_project_get.sql
-- Returns a single project header with access level.
-- Checks ownership first, then shared_objects for read access.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_get(p_project_id INT, p_user_id INT)
RETURNS TABLE(project_id INT, title TEXT, description TEXT, status TEXT, status_label TEXT, access TEXT, permission_level INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    -- Check ownership first
    RETURN QUERY
    SELECT p.project_id,
           p.title,
           p.description,
           ps.code::TEXT   AS status,
           ps.label::TEXT  AS status_label,
           'owned'::TEXT   AS access,
           3               AS permission_level
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
           p.description,
           ps.code::TEXT   AS status,
           ps.label::TEXT  AS status_label,
           'shared'::TEXT  AS access,
           so.permission_level::INT AS permission_level
      FROM public.shared_objects so
      JOIN public.projects p ON p.project_id = so.object_id
      JOIN public.project_statuses ps ON ps.status_id = p.status_id
     WHERE so.object_type_id = 1
       AND so.object_id = p_project_id
       AND so.shared_to_user_id = p_user_id
       AND so.permission_level >= 1;
END;
$proc$;
