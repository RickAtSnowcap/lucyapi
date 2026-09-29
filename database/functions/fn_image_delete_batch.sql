-- fn_image_delete_batch.sql
-- Deletes the given image rows that belong to the caller. Returns the count deleted.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_delete_batch(p_user_id INT, p_image_ids INT[])
RETURNS INT
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.images
     WHERE image_id = ANY(p_image_ids)
       AND user_id = p_user_id;
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END;
$proc$;
