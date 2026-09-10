-- fn_section_create.sql
-- Inserts a new project section and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_section_create(p_project_id INT, p_parent_id INT, p_title TEXT, p_description TEXT, p_file_path TEXT)
RETURNS TABLE(section_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.project_sections (project_id, parent_id, title, description, file_path)
    VALUES (p_project_id, p_parent_id, p_title, p_description, p_file_path)
    RETURNING project_sections.section_id, project_sections.title;
END;
$proc$;
