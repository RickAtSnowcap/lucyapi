-- fn_hint_get_compact.sql
-- Returns hint tree with titles only for quick discovery.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_get_compact(p_user_id INT)
RETURNS TABLE(hint_id INT, parent_id INT, title TEXT, sort_order INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- Owned hints
    SELECT h.hint_id,
           h.parent_id,
           h.title::TEXT,
           h.sort_order
      FROM public.hints h
     WHERE h.user_id = p_user_id

    UNION ALL

    -- Shared hints
    SELECT h.hint_id,
           h.parent_id,
           h.title::TEXT,
           h.sort_order
      FROM public.shared_objects so
      JOIN public.hints h ON h.hint_category_id = so.object_id
     WHERE so.shared_to_user_id = p_user_id
       AND so.object_type_id = 2

     ORDER BY parent_id, sort_order, hint_id;
END;
$proc$;
