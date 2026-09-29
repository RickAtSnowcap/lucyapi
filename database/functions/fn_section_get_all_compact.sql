-- fn_section_get_all_compact.sql
-- Returns section ids/parents/titles for a project (no descriptions).
-- Requires read access (fn_access_level >= 1); otherwise returns no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_get_all_compact(p_user_id INT, p_project_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 1 THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT ps.section_id,
           ps.parent_id,
           ps.title
      FROM public.project_sections ps
     WHERE ps.project_id = p_project_id
     ORDER BY ps.section_id;
END;
$proc$;
