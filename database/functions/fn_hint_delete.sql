-- fn_hint_delete.sql
-- Deletes a hint and all its descendants. Returns the count deleted.
-- Deleting a root category (the main entry) needs owner / admin access (level 3); deleting anything
-- below it needs edit access to the category (level 2). Otherwise returns 0.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_delete(p_user_id INT, p_hint_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_category_id INT;
    v_parent_id   INT;
    v_count       INT;
BEGIN
    SELECT h.hint_category_id, h.parent_id INTO v_category_id, v_parent_id FROM public.hints h WHERE h.hint_id = p_hint_id;
    -- A root category is the main entry (owner / admin, level 3); anything below it needs edit access (level 2).
    IF v_category_id IS NULL
       OR lucyapi.fn_access_level(p_user_id, 2::SMALLINT, v_category_id) < (CASE WHEN v_parent_id = 0 THEN 3 ELSE 2 END) THEN
        RETURN QUERY SELECT 0;
        RETURN;
    END IF;

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
