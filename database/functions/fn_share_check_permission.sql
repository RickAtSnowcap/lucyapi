-- fn_share_check_permission.sql
-- The caller's access to a specific object, for the check_access tool and GET /sharing/check.
-- Delegates to fn_access_level (the one place ownership + sharing is decided), so an owner gets (TRUE, 3).
-- Returns (FALSE, 0) if the user has no access or the object doesn't exist.

CREATE OR REPLACE FUNCTION lucyapi.fn_share_check_permission(p_user_id INT, p_object_type_id SMALLINT, p_object_id INT)
RETURNS TABLE(has_access BOOLEAN, permission_level SMALLINT)
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
AS $proc$
DECLARE
    v_level INT;
BEGIN
    v_level := lucyapi.fn_access_level(p_user_id, p_object_type_id, p_object_id);
    RETURN QUERY SELECT v_level > 0, v_level::SMALLINT;
END;
$proc$;
