-- fn_image_insert.sql
-- Inserts a new image record and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_insert(
    p_user_id    INT,
    p_filename   TEXT,
    p_prompt     TEXT,
    p_model      TEXT,
    p_size_bytes INT,
    p_width      INT,
    p_height     INT
)
RETURNS TABLE(image_id INT, filename TEXT, prompt TEXT, model TEXT,
              created_at TIMESTAMPTZ, keep BOOLEAN, size_bytes INT, width INT, height INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO images (user_id, filename, prompt, model, size_bytes, width, height)
    VALUES (p_user_id, p_filename, p_prompt, p_model, p_size_bytes, p_width, p_height)
    RETURNING images.image_id, images.filename, images.prompt, images.model,
              images.created_at, images.keep, images.size_bytes, images.width, images.height;
END;
$proc$;
