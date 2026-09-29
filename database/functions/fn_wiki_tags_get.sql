-- fn_wiki_tags_get.sql
-- Returns the distinct tags used in a wiki. Requires read access (fn_access_level >= 1); otherwise no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_tags_get(p_user_id INT, p_wiki_id INT)
RETURNS TABLE(tag TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 3::SMALLINT, p_wiki_id) < 1 THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT DISTINCT wst.tag::TEXT
      FROM public.wiki_section_tags wst
      JOIN public.wiki_sections ws ON ws.section_id = wst.section_id
     WHERE ws.wiki_id = p_wiki_id
     ORDER BY wst.tag::TEXT;
END;
$proc$;
