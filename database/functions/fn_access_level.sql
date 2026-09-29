-- fn_access_level.sql
-- The caller's access level to a shareable object — the ONE place ownership + sharing is decided.
--   3 = owner (or an admin-level share), 2 = read-write share, 1 = read-only share, 0 = no access.
-- Object types (object_types): 1 = project, 2 = hint category (object_id = the category's root hint_id), 3 = wiki.
-- p_user_id must always come from the authenticated caller (JWT / OAuth token / agent key), never from request input.
-- Required levels: read >= 1; create/edit/delete of sub-entries >= 2; delete of the main entry + share = 3.

CREATE OR REPLACE FUNCTION lucyapi.fn_access_level(p_user_id INT, p_object_type_id SMALLINT, p_object_id INT)
RETURNS INT
LANGUAGE plpgsql
STABLE
SECURITY INVOKER
AS $proc$
DECLARE
    v_owner INT;
    v_level INT;
BEGIN
    IF p_object_type_id = 1 THEN
        SELECT p.user_id INTO v_owner FROM public.projects p WHERE p.project_id = p_object_id;
    ELSIF p_object_type_id = 2 THEN
        SELECT h.user_id INTO v_owner FROM public.hints h WHERE h.hint_id = p_object_id AND h.parent_id = 0;
    ELSIF p_object_type_id = 3 THEN
        SELECT w.user_id INTO v_owner FROM public.wikis w WHERE w.wiki_id = p_object_id;
    END IF;

    IF v_owner IS NULL THEN
        RETURN 0;   -- no such object
    END IF;
    IF v_owner = p_user_id THEN
        RETURN 3;
    END IF;

    SELECT so.permission_level INTO v_level
      FROM public.shared_objects so
     WHERE so.shared_to_user_id = p_user_id
       AND so.object_type_id = p_object_type_id
       AND so.object_id = p_object_id;

    RETURN COALESCE(v_level, 0);
END;
$proc$;
