-- fn_wiki_delete.sql
-- Deletes a wiki and all its sections. Returns the count of sections deleted.
-- Requires owner / admin access (fn_access_level = 3); otherwise returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_delete(p_user_id INT, p_wiki_id INT)
RETURNS TABLE(sections_deleted INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 3::SMALLINT, p_wiki_id) < 3 THEN
        RETURN;
    END IF;

    DELETE FROM public.wiki_sections
     WHERE wiki_id = p_wiki_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    DELETE FROM public.wikis
     WHERE wiki_id = p_wiki_id;

    RETURN QUERY SELECT v_count;
END;
$proc$;
