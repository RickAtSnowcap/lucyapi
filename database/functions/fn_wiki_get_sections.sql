-- fn_wiki_get_sections.sql
-- Returns all sections for a wiki as a flat list with aggregated tags.
-- Tree building happens in the application layer.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_get_sections(p_wiki_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT, description TEXT, updated_at TIMESTAMPTZ, tags TEXT[])
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT ws.section_id,
           ws.parent_id,
           ws.title::TEXT,
           ws.description,
           ws.updated_at,
           COALESCE(
               array_agg(wst.tag::TEXT ORDER BY wst.tag) FILTER (WHERE wst.tag IS NOT NULL),
               '{}'::TEXT[]
           ) AS tags
      FROM public.wiki_sections ws
      LEFT JOIN public.wiki_section_tags wst ON wst.section_id = ws.section_id
     WHERE ws.wiki_id = p_wiki_id
     GROUP BY ws.section_id, ws.parent_id, ws.title, ws.description, ws.updated_at
     ORDER BY ws.parent_id, ws.section_id;
END;
$proc$;
