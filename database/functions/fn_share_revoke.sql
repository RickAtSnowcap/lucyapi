-- fn_share_revoke.sql
-- Revokes a share. Only the original sharer can revoke.

CREATE OR REPLACE FUNCTION lucyapi.fn_share_revoke(p_shared_by_user_id INT, p_share_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.shared_objects so
     WHERE so.share_id = p_share_id
       AND so.shared_by_user_id = p_shared_by_user_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
