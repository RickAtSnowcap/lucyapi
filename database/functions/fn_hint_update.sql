-- fn_hint_update.sql
-- Updates a hint using COALESCE to preserve fields not provided.
-- Requires edit access to the hint's category (fn_access_level >= 2); otherwise returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_update(p_user_id INT, p_hint_id INT, p_title TEXT DEFAULT NULL, p_description TEXT DEFAULT NULL, p_sort_order INT DEFAULT NULL)
RETURNS TABLE(hint_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_category_id INT;
BEGIN
    SELECT h.hint_category_id INTO v_category_id FROM public.hints h WHERE h.hint_id = p_hint_id;
    IF v_category_id IS NULL OR lucyapi.fn_access_level(p_user_id, 2::SMALLINT, v_category_id) < 2 THEN
        RETURN;
    END IF;

    RETURN QUERY
    UPDATE public.hints h
       SET title       = COALESCE(p_title, h.title),
           description = COALESCE(p_description, h.description),
           sort_order  = COALESCE(p_sort_order, h.sort_order),
           updated_at  = NOW()
     WHERE h.hint_id = p_hint_id
    RETURNING h.hint_id, h.title::TEXT;
END;
$proc$;
