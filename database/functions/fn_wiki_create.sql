-- fn_wiki_create.sql
-- Inserts a new wiki and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_create(p_user_id INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(wiki_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.wikis (user_id, title, description)
    VALUES (p_user_id, p_title, p_description)
    RETURNING wikis.wiki_id, wikis.title::TEXT;
END;
$proc$;
