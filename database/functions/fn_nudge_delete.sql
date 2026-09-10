-- fn_nudge_delete.sql
-- Deletes a nudge by user_id and nudge_id. Returns count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_nudge_delete(p_user_id INT, p_nudge_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.nudges n
     WHERE n.nudge_id = p_nudge_id
       AND n.user_id = p_user_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
