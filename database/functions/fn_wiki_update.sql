-- fn_wiki_update.sql
-- Updates a wiki's title/description. Requires edit access (fn_access_level >= 2); otherwise returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_update(p_user_id INT, p_wiki_id INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(wiki_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF lucyapi.fn_access_level(p_user_id, 3::SMALLINT, p_wiki_id) < 2 THEN
        RETURN;
    END IF;

    RETURN QUERY
    UPDATE public.wikis w
       SET title       = COALESCE(p_title, w.title),
           description = COALESCE(p_description, w.description),
           updated_at  = NOW()
     WHERE w.wiki_id = p_wiki_id
    RETURNING w.wiki_id, w.title::TEXT;
END;
$proc$;
