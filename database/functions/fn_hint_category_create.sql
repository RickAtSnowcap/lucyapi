-- fn_hint_category_create.sql
-- Creates a hint category. parent_id 0 = a new root owned by the caller; otherwise a nested
-- sub-category, which requires edit access to the parent's category (fn_access_level >= 2);
-- otherwise (or if the parent doesn't exist) returns no row.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_category_create(p_user_id INT, p_parent_id INT, p_title TEXT, p_description TEXT, p_sort_order INT DEFAULT 0)
RETURNS TABLE(hint_id INT, parent_id INT, title TEXT, hint_category_id INT, sort_order INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_hint_id     INT;
    v_category_id INT;
    v_owner_id    INT;
BEGIN
    IF COALESCE(p_parent_id, 0) = 0 THEN
        -- Root category, owned by the caller: insert then self-reference
        INSERT INTO public.hints (user_id, parent_id, title, description, hint_category_id, sort_order)
        VALUES (p_user_id, 0, p_title, p_description, 0, p_sort_order)
        RETURNING hints.hint_id INTO v_hint_id;

        UPDATE public.hints
           SET hint_category_id = v_hint_id
         WHERE hints.hint_id = v_hint_id;
    ELSE
        -- Child: must have edit access to the parent's category. The new row belongs to the
        -- category OWNER (the tree stays in one user's hints even when a share recipient writes to it).
        SELECT h.hint_category_id, h.user_id
          INTO v_category_id, v_owner_id
          FROM public.hints h
         WHERE h.hint_id = p_parent_id;

        IF v_category_id IS NULL OR lucyapi.fn_access_level(p_user_id, 2::SMALLINT, v_category_id) < 2 THEN
            RETURN;   -- missing parent or no edit access: not found
        END IF;

        INSERT INTO public.hints (user_id, parent_id, title, description, hint_category_id, sort_order)
        VALUES (v_owner_id, p_parent_id, p_title, p_description, v_category_id, p_sort_order)
        RETURNING hints.hint_id INTO v_hint_id;
    END IF;

    RETURN QUERY
    SELECT h.hint_id, h.parent_id, h.title::TEXT, h.hint_category_id, h.sort_order
      FROM public.hints h
     WHERE h.hint_id = v_hint_id;
END;
$proc$;
