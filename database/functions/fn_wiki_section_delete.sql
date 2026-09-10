-- fn_wiki_section_delete.sql
-- Deletes a wiki section and all descendants via recursive CTE.
-- Tags cascade automatically via FK ON DELETE CASCADE.
-- Touches parent wiki updated_at.
-- Returns total count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_section_delete(p_wiki_id INT, p_section_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
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
