-- fn_section_get.sql
-- Returns a section node and its direct children.
-- is_child = false for the requested node, true for children.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_get(p_project_id INT, p_section_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT, description TEXT, file_path TEXT, is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- The node itself
    SELECT ps.section_id,
           ps.parent_id,
           ps.title,
           ps.description,
           ps.file_path,
           FALSE AS is_child
      FROM public.project_sections ps
     WHERE ps.project_id = p_project_id
       AND ps.section_id = p_section_id

    UNION ALL

    -- Direct children
    SELECT ps.section_id,
           ps.parent_id,
           ps.title,
           ps.description,
           ps.file_path,
           TRUE AS is_child
      FROM public.project_sections ps
     WHERE ps.project_id = p_project_id
       AND ps.parent_id = p_section_id
     ORDER BY is_child, section_id;
END;
$proc$;
