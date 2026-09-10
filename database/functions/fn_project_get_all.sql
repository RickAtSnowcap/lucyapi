-- fn_project_get_all.sql
-- Returns owned projects UNION shared projects for a user.
-- Optional status_code filter applies to both owned and shared.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_get_all(p_user_id INT, p_status_code TEXT)
RETURNS TABLE(project_id INT, title TEXT, description TEXT, status TEXT, status_label TEXT, access TEXT, permission_level INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- Owned projects
    SELECT p.project_id,
           p.title,
           p.description,
           ps.code::TEXT   AS status,
           ps.label::TEXT  AS status_label,
           'owned'::TEXT   AS access,
           3               AS permission_level
      FROM public.projects p
      JOIN public.project_statuses ps ON ps.status_id = p.status_id
     WHERE p.user_id = p_user_id
       AND (p_status_code IS NULL OR ps.code = p_status_code)

    UNION ALL

    -- Shared projects
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
     WHERE so.shared_to_user_id = p_user_id
       AND so.object_type_id = 1
       AND (p_status_code IS NULL OR ps.code = p_status_code)

     ORDER BY project_id;
END;
$proc$;
