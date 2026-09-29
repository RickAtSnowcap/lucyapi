-- fn_wiki_section_delete.sql
-- Deletes a wiki section and its descendants. Returns the count deleted.
-- Requires edit access to the wiki (fn_access_level >= 2) — a level-2 share may delete sections;
-- only deleting the wiki itself needs level 3 (fn_wiki_delete). Otherwise returns 0.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_section_delete(p_user_id INT, p_wiki_id INT, p_section_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 3::SMALLINT, p_wiki_id) < 2 THEN
        RETURN QUERY SELECT 0;
        RETURN;
    END IF;

    WITH RECURSIVE subtree AS (
        SELECT ws.section_id
          FROM public.wiki_sections ws
         WHERE ws.wiki_id = p_wiki_id
           AND ws.section_id = p_section_id

        UNION ALL

        SELECT ws.section_id
          FROM public.wiki_sections ws
          JOIN subtree s ON s.section_id = ws.parent_id
         WHERE ws.wiki_id = p_wiki_id
    )
    DELETE FROM public.wiki_sections
     WHERE section_id IN (SELECT section_id FROM subtree);

    GET DIAGNOSTICS v_count = ROW_COUNT;

    UPDATE public.wikis SET updated_at = NOW() WHERE wiki_id = p_wiki_id;

    RETURN QUERY SELECT v_count;
END;
$proc$;
