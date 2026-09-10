-- fn_image_update_keep.sql
-- Updates the keep flag on an image and returns the updated row.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_update_keep(p_image_id INT, p_keep BOOLEAN)
RETURNS TABLE(image_id INT, filename TEXT, prompt TEXT, model TEXT,
              created_at TIMESTAMPTZ, keep BOOLEAN, size_bytes INT, width INT, height INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE images SET keep = p_keep
    WHERE images.image_id = p_image_id
    RETURNING images.image_id, images.filename, images.prompt, images.model,
              images.created_at, images.keep, images.size_bytes, images.width, images.height;
END;
$proc$;
