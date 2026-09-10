-- fn_wiki_get_all.sql
-- Returns owned wikis UNION shared wikis for a user.

CREATE OR REPLACE FUNCTION lucyapi.fn_wiki_get_all(p_user_id INT)
RETURNS TABLE(wiki_id INT, title TEXT, description TEXT, updated_at TIMESTAMPTZ, access TEXT, permission_level INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- Owned wikis
    SELECT w.wiki_id,
           w.title::TEXT,
           w.description,
           w.updated_at,
           'owned'::TEXT  AS access,
           3              AS permission_level
      FROM public.wikis w
     WHERE w.user_id = p_user_id

    UNION ALL

    -- Shared wikis
    SELECT w.wiki_id,
           w.title::TEXT,
           w.description,
           w.updated_at,
           'shared'::TEXT AS access,
           so.permission_level::INT AS permission_level
      FROM public.shared_objects so
      JOIN public.wikis w ON w.wiki_id = so.object_id
     WHERE so.shared_to_user_id = p_user_id
       AND so.object_type_id = 3

     ORDER BY wiki_id;
END;
$proc$;
