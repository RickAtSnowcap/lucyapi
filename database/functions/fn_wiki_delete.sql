-- fn_wiki_delete.sql
-- Deletes a wiki and all its sections.
-- Tags cascade automatically via FK ON DELETE CASCADE on wiki_sections.
-- Returns the count of sections deleted.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_delete(p_wiki_id INT)
RETURNS TABLE(sections_deleted INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.wiki_sections
     WHERE wiki_id = p_wiki_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    DELETE FROM public.wikis
     WHERE wiki_id = p_wiki_id;

    RETURN QUERY SELECT v_count;
END;
$proc$;
