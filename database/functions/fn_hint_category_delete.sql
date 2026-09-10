-- fn_hint_category_delete.sql
-- Deletes a hint category only if it has no children.
-- Raises an exception if children exist to prevent orphaning.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_category_delete(p_hint_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    IF EXISTS (SELECT 1 FROM public.hints WHERE parent_id = p_hint_id) THEN
        RAISE EXCEPTION 'Cannot delete category % — it has children. Delete children first.', p_hint_id;
    END IF;

    DELETE FROM public.hints
     WHERE hint_id = p_hint_id;

    RETURN QUERY SELECT 1;
END;
$proc$;
