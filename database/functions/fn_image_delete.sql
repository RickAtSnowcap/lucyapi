-- fn_image_delete.sql
-- Deletes an image record. Returns filename and whether deletion occurred.
-- Respects keep flag unless force=true.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_delete(p_image_id INT, p_force BOOLEAN DEFAULT FALSE)
RETURNS TABLE(image_id INT, filename TEXT, deleted BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_filename TEXT;
    v_keep     BOOLEAN;
BEGIN
    SELECT i.filename, i.keep INTO v_filename, v_keep
    FROM images i WHERE i.image_id = p_image_id;

    IF v_filename IS NULL THEN
        RETURN;
    END IF;

    IF v_keep AND NOT p_force THEN
        RETURN QUERY SELECT p_image_id, v_filename, FALSE;
        RETURN;
    END IF;

    DELETE FROM images WHERE images.image_id = p_image_id;
    RETURN QUERY SELECT p_image_id, v_filename, TRUE;
END;
$proc$;
