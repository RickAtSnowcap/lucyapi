-- fn_section_create.sql
-- Creates a section under a project (parent_id 0 = top level).
-- Requires edit access (fn_access_level >= 2), and a non-zero parent must be a section of the SAME project;
-- otherwise returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_create(p_user_id INT, p_project_id INT, p_parent_id INT, p_title TEXT, p_description TEXT, p_file_path TEXT)
RETURNS TABLE(section_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 1::SMALLINT, p_project_id) < 2 THEN
        RETURN;
    END IF;
    IF COALESCE(p_parent_id, 0) <> 0 AND NOT EXISTS (
        SELECT 1 FROM public.project_sections ps
         WHERE ps.section_id = p_parent_id AND ps.project_id = p_project_id) THEN
        RETURN;
    END IF;

    RETURN QUERY
    INSERT INTO public.project_sections (project_id, parent_id, title, description, file_path)
    VALUES (p_project_id, COALESCE(p_parent_id, 0), p_title, p_description, p_file_path)
    RETURNING project_sections.section_id, project_sections.title;
END;
$proc$;
