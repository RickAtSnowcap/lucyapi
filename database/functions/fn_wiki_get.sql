-- fn_wiki_get.sql
-- Returns a single wiki header with access level.
-- Checks ownership first, then shared_objects for read access.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_get(p_wiki_id INT, p_user_id INT)
RETURNS TABLE(wiki_id INT, title TEXT, description TEXT, updated_at TIMESTAMPTZ, access TEXT, permission_level INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    -- Check ownership first
    RETURN QUERY
    SELECT w.wiki_id,
           w.title::TEXT,
           w.description,
           w.updated_at,
           'owned'::TEXT AS access,
           3             AS permission_level
      FROM public.wikis w
     WHERE w.wiki_id = p_wiki_id
       AND w.user_id = p_user_id;

    IF FOUND THEN
        RETURN;
    END IF;

    -- Check shared access
    RETURN QUERY
    SELECT w.wiki_id,
           w.title::TEXT,
           w.description,
           w.updated_at,
           'shared'::TEXT AS access,
           so.permission_level::INT AS permission_level
      FROM public.shared_objects so
      JOIN public.wikis w ON w.wiki_id = so.object_id
     WHERE so.object_type_id = 3
       AND so.object_id = p_wiki_id
       AND so.shared_to_user_id = p_user_id
       AND so.permission_level >= 1;
END;
$proc$;
