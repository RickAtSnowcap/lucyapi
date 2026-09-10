-- fn_section_get_all_compact.sql
-- Returns all sections for a project with titles only (no description, no file_path).
-- Flat list ordered by section_id ascending (natural creation order).

CREATE OR REPLACE FUNCTION lucyapi.fn_section_get_all_compact(p_project_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT ps.section_id,
           ps.parent_id,
           ps.title
      FROM public.project_sections ps
     WHERE ps.project_id = p_project_id
     ORDER BY ps.section_id;
END;
$proc$;
