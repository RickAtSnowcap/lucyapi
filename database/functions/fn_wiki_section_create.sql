-- fn_wiki_section_create.sql
-- Inserts a new wiki section with optional tags.
-- Touches parent wiki updated_at.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_section_create(p_wiki_id INT, p_parent_id INT, p_title TEXT, p_description TEXT, p_tags TEXT[])
RETURNS TABLE(section_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_section_id INT;
BEGIN
    INSERT INTO public.wiki_sections (wiki_id, parent_id, title, description)
    VALUES (p_wiki_id, p_parent_id, p_title, p_description)
    RETURNING wiki_sections.section_id INTO v_section_id;

    IF p_tags IS NOT NULL AND array_length(p_tags, 1) > 0 THEN
        INSERT INTO public.wiki_section_tags (section_id, tag)
        SELECT v_section_id, unnest(p_tags);
    END IF;

    UPDATE public.wikis SET updated_at = NOW() WHERE wiki_id = p_wiki_id;

    RETURN QUERY SELECT v_section_id, p_title;
END;
$proc$;
