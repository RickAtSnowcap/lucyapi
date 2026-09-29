-- fn_image_delete.sql
-- Deletes an image row (the app deletes the file). Kept images need p_force.
-- Only the caller's own images; anything else returns no row (not found).

CREATE OR REPLACE FUNCTION lucyapi.fn_image_delete(p_user_id INT, p_image_id INT, p_force BOOLEAN DEFAULT FALSE)
RETURNS TABLE(image_id INT, filename TEXT, deleted BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_filename TEXT;
    v_keep     BOOLEAN;
BEGIN
    SELECT i.filename, i.keep INTO v_filename, v_keep
      FROM public.images i
     WHERE i.image_id = p_image_id
       AND i.user_id = p_user_id;

    IF v_filename IS NULL THEN
        RETURN;
    END IF;

    IF v_keep AND NOT p_force THEN
        RETURN QUERY SELECT p_image_id, v_filename, FALSE;
        RETURN;
    END IF;

    DELETE FROM public.images WHERE images.image_id = p_image_id AND images.user_id = p_user_id;

    RETURN QUERY SELECT p_image_id, v_filename, TRUE;
END;
$proc$;
