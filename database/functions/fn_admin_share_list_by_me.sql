-- fn_admin_share_list_by_me.sql
-- Shares the caller has granted, with the recipient's name and the object's title (admin Sharing page).

CREATE OR REPLACE FUNCTION lucyapi.fn_admin_share_list_by_me(p_user_id INT)
RETURNS TABLE(share_id INT, object_type_id SMALLINT, object_type TEXT, object_id INT,
              shared_to_user_id INT, shared_to_name TEXT, shared_by_user_id INT, shared_by_name TEXT,
              permission_level SMALLINT, object_title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT so.share_id, so.object_type_id, ot.name::TEXT, so.object_id,
           so.shared_to_user_id, u.name, NULL::INT, NULL::TEXT,
           so.permission_level,
           COALESCE(p.title, h.title, w.title)::TEXT
      FROM public.shared_objects so
      JOIN public.object_types ot ON ot.object_type_id = so.object_type_id
      JOIN public.users u ON u.user_id = so.shared_to_user_id
      LEFT JOIN public.projects p ON so.object_type_id = 1 AND p.project_id = so.object_id
      LEFT JOIN public.hints h    ON so.object_type_id = 2 AND h.hint_id = so.object_id
      LEFT JOIN public.wikis w    ON so.object_type_id = 3 AND w.wiki_id = so.object_id
     WHERE so.shared_by_user_id = p_user_id
     ORDER BY so.object_type_id, so.object_id;
END;
$proc$;
