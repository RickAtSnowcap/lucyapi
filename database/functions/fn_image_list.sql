-- fn_image_list.sql
-- Lists images with optional user and keep filters, ordered by newest first.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_list(
    p_user_id INT DEFAULT NULL,
    p_keep    BOOLEAN DEFAULT NULL,
    p_limit   INT DEFAULT 50,
    p_offset  INT DEFAULT 0
)
RETURNS TABLE(image_id INT, filename TEXT, prompt TEXT, model TEXT,
              created_at TIMESTAMPTZ, keep BOOLEAN, size_bytes INT, width INT, height INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT i.image_id, i.filename, i.prompt, i.model,
           i.created_at, i.keep, i.size_bytes, i.width, i.height
    FROM images i
    WHERE (p_user_id IS NULL OR i.user_id = p_user_id)
      AND (p_keep IS NULL OR i.keep = p_keep)
    ORDER BY i.created_at DESC
    LIMIT p_limit OFFSET p_offset;
END;
$proc$;
