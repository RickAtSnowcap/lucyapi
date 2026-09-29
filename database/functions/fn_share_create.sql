-- fn_share_create.sql
-- Shares an object with another user. The sharer must be its owner or hold an admin-level share
-- (fn_access_level = 3); for hints the object must be a category root. Otherwise returns no row.
-- A duplicate share raises (unique constraint); self-share is blocked by chk_not_self_share.

CREATE OR REPLACE FUNCTION lucyapi.fn_share_create(
    p_shared_by_user_id INT,
    p_shared_to_user_id INT,
    p_object_type_id SMALLINT,
    p_object_id INT,
    p_permission_level SMALLINT
)
RETURNS TABLE(share_id INT, shared_to_user_id INT, object_type_id SMALLINT, object_id INT, permission_level SMALLINT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
#variable_conflict use_column
BEGIN
    IF lucyapi.fn_access_level(p_shared_by_user_id, p_object_type_id, p_object_id) < 3 THEN
        RETURN;
    END IF;

    RETURN QUERY
    INSERT INTO public.shared_objects (shared_by_user_id, shared_to_user_id, object_type_id, object_id, permission_level)
    VALUES (p_shared_by_user_id, p_shared_to_user_id, p_object_type_id, p_object_id, p_permission_level)
    RETURNING shared_objects.share_id,
              shared_objects.shared_to_user_id,
              shared_objects.object_type_id,
              shared_objects.object_id,
              shared_objects.permission_level;
END;
$proc$;
