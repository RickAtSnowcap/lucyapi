-- fn_hint_get.sql
-- Returns a hint node and its direct children.
-- is_child = false for the requested node, true for children.
-- Includes user_id for app-layer ownership checks.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_get(p_hint_id INT)
RETURNS TABLE(hint_id INT, parent_id INT, title TEXT, description TEXT, hint_category_id INT, sort_order INT, user_id INT, is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- The node itself
    SELECT h.hint_id,
           h.parent_id,
           h.title::TEXT,
           h.description,
           h.hint_category_id,
           h.sort_order,
           h.user_id,
           FALSE AS is_child
      FROM public.hints h
     WHERE h.hint_id = p_hint_id

    UNION ALL

    -- Direct children
    SELECT h.hint_id,
           h.parent_id,
           h.title::TEXT,
           h.description,
           h.hint_category_id,
           h.sort_order,
           h.user_id,
           TRUE AS is_child
      FROM public.hints h
     WHERE h.parent_id = p_hint_id
     ORDER BY is_child, sort_order, hint_id;
END;
$proc$;
