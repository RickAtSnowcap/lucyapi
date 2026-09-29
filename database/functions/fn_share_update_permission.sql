-- fn_share_update_permission.sql
-- Changes a share's permission level — only a share the caller granted. Returns no row otherwise.

CREATE OR REPLACE FUNCTION lucyapi.fn_share_update_permission(p_user_id INT, p_share_id INT, p_permission_level SMALLINT)
RETURNS TABLE(share_id INT, object_type_id INT, object_id INT, shared_to_user_id INT, permission_level INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.shared_objects so
       SET permission_level = p_permission_level
     WHERE so.share_id = p_share_id
       AND so.shared_by_user_id = p_user_id
    RETURNING so.share_id, so.object_type_id::INT, so.object_id, so.shared_to_user_id, so.permission_level::INT;
END;
$proc$;
