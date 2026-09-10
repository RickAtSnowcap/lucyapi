-- fn_wiki_update.sql
-- Updates a wiki using COALESCE to preserve fields not provided.
-- Sets updated_at = NOW() on every update.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_update(p_wiki_id INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(wiki_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.wikis w
       SET title       = COALESCE(p_title, w.title),
           description = COALESCE(p_description, w.description),
           updated_at  = NOW()
     WHERE w.wiki_id = p_wiki_id
    RETURNING w.wiki_id, w.title::TEXT;
END;
$proc$;
