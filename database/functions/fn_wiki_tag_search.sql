-- fn_wiki_tag_search.sql
-- Finds all sections across a user's wikis (owned + shared) that have the given tag.
-- Returns full tag arrays for each matching section.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_tag_search(p_tag TEXT, p_user_id INT)
RETURNS TABLE(wiki_id INT, wiki_title TEXT, section_id INT, title TEXT, description TEXT, updated_at TIMESTAMPTZ, tags TEXT[])
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT w.wiki_id,
           w.title::TEXT AS wiki_title,
           ws.section_id,
           ws.title::TEXT,
           ws.description,
           ws.updated_at,
           COALESCE(
               array_agg(all_tags.tag::TEXT ORDER BY all_tags.tag) FILTER (WHERE all_tags.tag IS NOT NULL),
               '{}'::TEXT[]
           ) AS tags
      FROM public.wiki_section_tags wst
      JOIN public.wiki_sections ws ON ws.section_id = wst.section_id
      JOIN public.wikis w ON w.wiki_id = ws.wiki_id
      LEFT JOIN public.wiki_section_tags all_tags ON all_tags.section_id = ws.section_id
     WHERE wst.tag = p_tag
       AND (
           w.user_id = p_user_id
           OR EXISTS (
               SELECT 1 FROM public.shared_objects so
                WHERE so.object_type_id = 3
                  AND so.object_id = w.wiki_id
                  AND so.shared_to_user_id = p_user_id
           )
       )
     GROUP BY w.wiki_id, w.title, ws.section_id, ws.title, ws.description, ws.updated_at
     ORDER BY w.wiki_id, ws.section_id;
END;
$proc$;
