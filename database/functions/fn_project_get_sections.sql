-- fn_project_get_sections.sql
-- Returns all sections for a project as a flat list (tree built in the app layer).
-- Requires read access (fn_access_level >= 1); otherwise returns no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_get_sections(p_user_id INT, p_project_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT, description TEXT, file_path TEXT)
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
           ps.title,
           ps.description,
           ps.file_path
      FROM public.project_sections ps
     WHERE ps.project_id = p_project_id
     ORDER BY ps.parent_id, ps.section_id;
END;
$proc$;
