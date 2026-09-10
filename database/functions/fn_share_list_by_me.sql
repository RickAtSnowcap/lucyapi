-- fn_share_list_by_me.sql
-- Lists all shares created by a user, with recipient username and object type name.

CREATE OR REPLACE FUNCTION lucyapi.fn_share_list_by_me(p_user_id INT)
RETURNS TABLE(share_id INT, shared_to_user_id INT, shared_to_username TEXT, object_type_id SMALLINT, object_type_name TEXT, object_id INT, permission_level SMALLINT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
#variable_conflict use_column
BEGIN
    RETURN QUERY
    SELECT so.share_id,
           so.shared_to_user_id,
           u.username::TEXT,
           so.object_type_id,
           ot.name::TEXT,
           so.object_id,
           so.permission_level
      FROM public.shared_objects so
      JOIN public.users u ON u.user_id = so.shared_to_user_id
      JOIN public.object_types ot ON ot.object_type_id = so.object_type_id
     WHERE so.shared_by_user_id = p_user_id
     ORDER BY so.object_type_id, so.object_id;
END;
$proc$;
