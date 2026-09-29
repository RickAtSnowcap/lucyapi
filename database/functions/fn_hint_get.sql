-- fn_hint_get.sql
-- Returns a hint and its direct children.
-- Requires read access to the hint's category (fn_access_level >= 1); otherwise no rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_get(p_user_id INT, p_hint_id INT)
RETURNS TABLE(hint_id INT, parent_id INT, title TEXT, description TEXT, hint_category_id INT, sort_order INT, user_id INT, is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_category_id INT;
BEGIN
    SELECT h.hint_category_id INTO v_category_id FROM public.hints h WHERE h.hint_id = p_hint_id;
    IF v_category_id IS NULL OR lucyapi.fn_access_level(p_user_id, 2::SMALLINT, v_category_id) < 1 THEN
        RETURN;
    END IF;

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
