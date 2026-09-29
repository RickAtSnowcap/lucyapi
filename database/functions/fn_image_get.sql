-- fn_image_get.sql
-- Returns one image's metadata — only if it belongs to the caller.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_get(p_user_id INT, p_image_id INT)
RETURNS TABLE(image_id INT, filename TEXT, prompt TEXT, model TEXT,
              created_at TIMESTAMPTZ, keep BOOLEAN, size_bytes INT, width INT, height INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT i.image_id, i.filename, i.prompt, i.model,
           i.created_at, i.keep, i.size_bytes, i.width, i.height
      FROM public.images i
     WHERE i.image_id = p_image_id
       AND i.user_id = p_user_id;
END;
$proc$;
