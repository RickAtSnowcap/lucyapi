-- fn_hint_create.sql
-- Creates a hint. If parent_id=0, creates a root category (self-referencing).
-- If parent_id>0, inherits hint_category_id and user_id from parent.

CREATE OR REPLACE FUNCTION lucyapi.fn_hint_create(p_user_id INT, p_parent_id INT, p_title TEXT, p_description TEXT, p_sort_order INT DEFAULT 0)
RETURNS TABLE(hint_id INT, parent_id INT, title TEXT, hint_category_id INT, sort_order INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_hint_id INT;
    v_category_id INT;
    v_owner_id INT;
BEGIN
    IF p_parent_id = 0 THEN
        -- Root category: insert then self-reference
        INSERT INTO public.hints (user_id, parent_id, title, description, hint_category_id, sort_order)
        VALUES (p_user_id, 0, p_title, p_description, 0, p_sort_order)
        RETURNING hints.hint_id INTO v_hint_id;

        UPDATE public.hints
           SET hint_category_id = v_hint_id
         WHERE hints.hint_id = v_hint_id;

        RETURN QUERY
        SELECT h.hint_id, h.parent_id, h.title::TEXT, h.hint_category_id, h.sort_order
          FROM public.hints h
         WHERE h.hint_id = v_hint_id;
    ELSE
        -- Child hint: inherit from parent
        SELECT h.hint_category_id, h.user_id
          INTO v_category_id, v_owner_id
          FROM public.hints h
         WHERE h.hint_id = p_parent_id;

        IF v_category_id IS NULL THEN
            RAISE EXCEPTION 'Parent hint % not found', p_parent_id;
        END IF;

        RETURN QUERY
        INSERT INTO public.hints (user_id, parent_id, title, description, hint_category_id, sort_order)
        VALUES (v_owner_id, p_parent_id, p_title, p_description, v_category_id, p_sort_order)
        RETURNING hints.hint_id, hints.parent_id, hints.title::TEXT, hints.hint_category_id, hints.sort_order;
    END IF;
END;
$proc$;
