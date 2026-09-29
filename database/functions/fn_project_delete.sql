-- fn_project_delete.sql
-- Deletes a project and all its sections. Returns the count of sections deleted.
-- Requires owner / admin access (fn_access_level = 3); otherwise returns no row (treated as not found).

CREATE OR REPLACE FUNCTION lucyapi.fn_project_delete(p_user_id INT, p_project_id INT)
RETURNS TABLE(sections_deleted INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 3 THEN
        RETURN;
    END IF;

    DELETE FROM public.project_sections
     WHERE project_id = p_project_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    DELETE FROM public.projects
     WHERE project_id = p_project_id;

    RETURN QUERY SELECT v_count;
END;
$proc$;
