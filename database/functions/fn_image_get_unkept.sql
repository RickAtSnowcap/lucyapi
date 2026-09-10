-- fn_image_get_unkept.sql
-- Returns image IDs and filenames where keep=false, for bulk cleanup.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_get_unkept(p_user_id INT DEFAULT NULL)
RETURNS TABLE(image_id INT, filename TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT i.image_id, i.filename
    FROM images i
    WHERE i.keep = FALSE
      AND (p_user_id IS NULL OR i.user_id = p_user_id);
END;
$proc$;
