-- fn_wiki_section_create.sql
-- Creates a wiki section (parent_id 0 = top level) with optional tags.
-- Requires edit access (fn_access_level >= 2), and a non-zero parent must be a section of the SAME wiki;
-- otherwise returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_section_create(p_user_id INT, p_wiki_id INT, p_parent_id INT, p_title TEXT, p_description TEXT, p_tags TEXT[])
RETURNS TABLE(section_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_section_id INT;
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 3::SMALLINT, p_wiki_id) < 2 THEN
        RETURN;
    END IF;
    IF COALESCE(p_parent_id, 0) <> 0 AND NOT EXISTS (
        SELECT 1 FROM public.wiki_sections ws
         WHERE ws.section_id = p_parent_id AND ws.wiki_id = p_wiki_id) THEN
        RETURN;
    END IF;

    INSERT INTO public.wiki_sections (wiki_id, parent_id, title, description)
    VALUES (p_wiki_id, COALESCE(p_parent_id, 0), p_title, p_description)
    RETURNING wiki_sections.section_id INTO v_section_id;

    IF p_tags IS NOT NULL AND array_length(p_tags, 1) > 0 THEN
        INSERT INTO public.wiki_section_tags (section_id, tag)
        SELECT v_section_id, unnest(p_tags);
    END IF;

    UPDATE public.wikis SET updated_at = NOW() WHERE wiki_id = p_wiki_id;

    RETURN QUERY SELECT v_section_id, p_title;
END;
$proc$;
