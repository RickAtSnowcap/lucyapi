-- fn_image_insert.sql
-- Inserts a new image record and returns the created row.
-- p_agent_id is the agent that created it (from the OAuth token), NULL for LucyAdmin uploads.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_insert(
    p_user_id     INT,
    p_agent_id    INT,
    p_filename    TEXT,
    p_source      TEXT,
    p_mime_type   TEXT,
    p_title       TEXT,
    p_description TEXT,
    p_prompt      TEXT,
    p_model       TEXT,
    p_keep        BOOLEAN,
    p_size_bytes  INT,
    p_width       INT,
    p_height      INT
)
RETURNS TABLE(image_id INT, filename TEXT, prompt TEXT, model TEXT,
              created_at TIMESTAMPTZ, keep BOOLEAN, size_bytes INT, width INT, height INT,
              title TEXT, description TEXT, mime_type TEXT, source TEXT, agent_id INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.images (user_id, agent_id, filename, source, mime_type, title, description,
                               prompt, model, keep, size_bytes, width, height)
    VALUES (p_user_id, p_agent_id, p_filename, p_source, p_mime_type, p_title, p_description,
            p_prompt, p_model, COALESCE(p_keep, FALSE), p_size_bytes, p_width, p_height)
    RETURNING images.image_id, images.filename, images.prompt, images.model,
              images.created_at, images.keep, images.size_bytes, images.width, images.height,
              images.title, images.description, images.mime_type, images.source, images.agent_id;
END;
$proc$;
