-- fn_secret_delete.sql
-- Deletes a secret by user_id + key. Returns count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_secret_delete(p_user_id INT, p_key TEXT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.secrets s
     WHERE s.user_id = p_user_id
       AND s.key = p_key;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
