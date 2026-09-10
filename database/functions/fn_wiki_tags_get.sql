-- fn_wiki_tags_get.sql
-- Returns all distinct tags for a wiki, sorted alphabetically.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_tags_get(p_wiki_id INT)
RETURNS TABLE(tag TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT DISTINCT wst.tag::TEXT
      FROM public.wiki_section_tags wst
      JOIN public.wiki_sections ws ON ws.section_id = wst.section_id
     WHERE ws.wiki_id = p_wiki_id
     ORDER BY wst.tag::TEXT;
END;
$proc$;
