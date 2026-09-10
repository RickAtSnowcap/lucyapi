-- fn_hint_delete.sql
-- Deletes a hint and all descendants via recursive CTE.
-- Returns total count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_delete(p_hint_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    WITH RECURSIVE subtree AS (
        SELECT h.hint_id
          FROM public.hints h
         WHERE h.hint_id = p_hint_id

        UNION ALL

        SELECT h.hint_id
          FROM public.hints h
          JOIN subtree s ON s.hint_id = h.parent_id
    )
    DELETE FROM public.hints
     WHERE hint_id IN (SELECT hint_id FROM subtree);

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
