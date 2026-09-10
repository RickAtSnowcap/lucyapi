-- fn_hint_update.sql
-- Updates a hint using COALESCE to preserve fields not provided.
-- Sets updated_at = NOW() on every update.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_update(p_hint_id INT, p_title TEXT DEFAULT NULL, p_description TEXT DEFAULT NULL, p_sort_order INT DEFAULT NULL)
RETURNS TABLE(hint_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
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
