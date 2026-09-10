-- fn_project_get_sections.sql
-- Returns all sections for a project as a flat list.
-- Tree building happens in the application layer.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_get_sections(p_project_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT, description TEXT, file_path TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
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
