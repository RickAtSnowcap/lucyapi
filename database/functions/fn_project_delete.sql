-- fn_project_delete.sql
-- Deletes a project and all its sections.
-- Returns the count of sections deleted.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_delete(p_project_id INT)
RETURNS TABLE(sections_deleted INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.project_sections
     WHERE project_id = p_project_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    DELETE FROM public.projects
     WHERE project_id = p_project_id;

    RETURN QUERY SELECT v_count;
END;
$proc$;
