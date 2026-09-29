-- fn_wiki_section_get.sql
-- Returns a wiki section and its direct children, with tags.
-- Requires read access to the wiki (fn_access_level >= 1); otherwise no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_section_get(p_user_id INT, p_wiki_id INT, p_section_id INT)
RETURNS TABLE(section_id INT, parent_id INT, title TEXT, description TEXT, updated_at TIMESTAMPTZ, tags TEXT[], is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 3::SMALLINT, p_wiki_id) < 1 THEN
        RETURN;
    END IF;

    RETURN QUERY
    WITH targets AS (
        SELECT ws.section_id, ws.parent_id, ws.title::TEXT, ws.description, ws.updated_at,
               FALSE AS is_child
          FROM public.wiki_sections ws
         WHERE ws.wiki_id = p_wiki_id
           AND ws.section_id = p_section_id

        UNION ALL

        SELECT ws.section_id, ws.parent_id, ws.title::TEXT, ws.description, ws.updated_at,
               TRUE AS is_child
          FROM public.wiki_sections ws
         WHERE ws.wiki_id = p_wiki_id
           AND ws.parent_id = p_section_id
    )
    SELECT t.section_id,
           t.parent_id,
           t.title,
           t.description,
           t.updated_at,
           COALESCE(
               array_agg(wst.tag::TEXT ORDER BY wst.tag) FILTER (WHERE wst.tag IS NOT NULL),
               '{}'::TEXT[]
           ) AS tags,
           t.is_child
      FROM targets t
      LEFT JOIN public.wiki_section_tags wst ON t.section_id = wst.section_id
     GROUP BY t.section_id, t.parent_id, t.title, t.description, t.updated_at, t.is_child
     ORDER BY t.is_child, t.section_id;
END;
$proc$;
