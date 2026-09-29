-- fn_section_update.sql
-- Updates a section using COALESCE to preserve fields not provided.
-- Requires edit access to the project (fn_access_level >= 2); otherwise returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_update(p_user_id INT, p_project_id INT, p_section_id INT, p_title TEXT, p_description TEXT, p_file_path TEXT)
RETURNS TABLE(section_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 2 THEN
        RETURN;
    END IF;

    RETURN QUERY
    UPDATE public.project_sections ps
       SET title       = COALESCE(p_title, ps.title),
           description = COALESCE(p_description, ps.description),
           file_path   = COALESCE(p_file_path, ps.file_path),
           updated_at  = NOW()
     WHERE ps.project_id = p_project_id
       AND ps.section_id = p_section_id
    RETURNING ps.section_id, ps.title;
END;
$proc$;
