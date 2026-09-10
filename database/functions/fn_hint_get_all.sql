-- fn_hint_get_all.sql
-- Returns owned hints UNION shared hint categories for a user.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_get_all(p_user_id INT)
RETURNS TABLE(hint_id INT, parent_id INT, title TEXT, description TEXT, hint_category_id INT, sort_order INT, access TEXT, permission_level SMALLINT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- Owned hints
    SELECT h.hint_id,
           h.parent_id,
           h.title::TEXT,
           h.description,
           h.hint_category_id,
           h.sort_order,
           'owned'::TEXT      AS access,
           3::SMALLINT        AS permission_level
      FROM public.hints h
     WHERE h.user_id = p_user_id

    UNION ALL

    -- Shared hints (via shared hint categories, object_type_id = 2)
    SELECT h.hint_id,
           h.parent_id,
           h.title::TEXT,
           h.description,
           h.hint_category_id,
           h.sort_order,
           'shared'::TEXT     AS access,
           so.permission_level AS permission_level
      FROM public.shared_objects so
      JOIN public.hints h ON h.hint_category_id = so.object_id
     WHERE so.shared_to_user_id = p_user_id
       AND so.object_type_id = 2

     ORDER BY parent_id, sort_order, hint_id;
END;
$proc$;
