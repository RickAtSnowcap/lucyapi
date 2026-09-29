-- fn_section_get.sql
-- Returns a section and its direct children.
-- Requires read access to the project (fn_access_level >= 1); otherwise returns no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_get(p_user_id INT, p_project_id INT, p_section_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT, description TEXT, file_path TEXT, is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 1 THEN
        RETURN;
    END IF;

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
