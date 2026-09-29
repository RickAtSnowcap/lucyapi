-- fn_section_delete.sql
-- Deletes a section and all its descendants. Returns the count deleted.
-- Requires edit access to the project (fn_access_level >= 2) — a level-2 share may delete sections;
-- only deleting the project itself needs level 3 (fn_project_delete). Otherwise returns 0.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_delete(p_user_id INT, p_project_id INT, p_section_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 2 THEN
        RETURN QUERY SELECT 0;
        RETURN;
    END IF;

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
