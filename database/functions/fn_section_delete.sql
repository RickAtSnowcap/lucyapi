-- fn_section_delete.sql
-- Deletes a section and all descendants via recursive CTE.
-- Returns total count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_delete(p_project_id INT, p_section_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    WITH RECURSIVE subtree AS (
        SELECT ps.section_id
          FROM public.project_sections ps
         WHERE ps.project_id = p_project_id
           AND ps.section_id = p_section_id

        UNION ALL

        SELECT ps.section_id
          FROM public.project_sections ps
          JOIN subtree s ON s.section_id = ps.parent_id
         WHERE ps.project_id = p_project_id
    )
    DELETE FROM public.project_sections
     WHERE section_id IN (SELECT section_id FROM subtree);

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
