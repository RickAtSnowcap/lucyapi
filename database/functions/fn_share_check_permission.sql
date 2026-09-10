-- fn_share_check_permission.sql
-- Checks if a user has shared access to a specific object.
-- Returns (FALSE, 0) if no access found.

CREATE OR REPLACE FUNCTION lucyapi.fn_share_check_permission(p_user_id INT, p_object_type_id SMALLINT, p_object_id INT)
RETURNS TABLE(has_access BOOLEAN, permission_level SMALLINT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_permission SMALLINT;
BEGIN
    SELECT so.permission_level
      INTO v_permission
      FROM public.shared_objects so
     WHERE so.shared_to_user_id = p_user_id
       AND so.object_type_id = p_object_type_id
       AND so.object_id = p_object_id;

    IF FOUND THEN
        RETURN QUERY SELECT TRUE, v_permission;
    ELSE
        RETURN QUERY SELECT FALSE, 0::SMALLINT;
    END IF;
END;
$proc$;
