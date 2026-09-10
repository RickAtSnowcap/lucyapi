-- fn_wiki_section_update.sql
-- Updates a wiki section using COALESCE to preserve fields not provided.
-- Tag handling: NULL = don't touch, '{}' = clear all, non-empty = replace all.
-- Touches parent wiki updated_at.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_section_update(p_wiki_id INT, p_section_id INT, p_title TEXT, p_description TEXT, p_tags TEXT[])
RETURNS TABLE(section_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    UPDATE public.wiki_sections ws
       SET title       = COALESCE(p_title, ws.title),
           description = COALESCE(p_description, ws.description),
           updated_at  = NOW()
     WHERE ws.wiki_id = p_wiki_id
       AND ws.section_id = p_section_id;

    -- Tag handling: NULL = skip, empty array = clear, non-empty = replace
    IF p_tags IS NOT NULL THEN
        DELETE FROM public.wiki_section_tags wst WHERE wst.section_id = p_section_id;

        IF array_length(p_tags, 1) > 0 THEN
            INSERT INTO public.wiki_section_tags (section_id, tag)
            SELECT p_section_id, unnest(p_tags);
        END IF;
    END IF;

    UPDATE public.wikis SET updated_at = NOW() WHERE wiki_id = p_wiki_id;

    RETURN QUERY
    SELECT ws.section_id, ws.title::TEXT
      FROM public.wiki_sections ws
     WHERE ws.wiki_id = p_wiki_id
       AND ws.section_id = p_section_id;
END;
$proc$;
