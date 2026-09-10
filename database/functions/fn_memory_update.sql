-- fn_memory_update.sql
-- Updates a memory using COALESCE to preserve fields not provided.
-- No updated_at column on memories table.

CREATE OR REPLACE FUNCTION lucyapi.fn_memory_update(p_agent_id INT, p_pkid INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(pkid INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.memories m
       SET title       = COALESCE(p_title, m.title),
           description = COALESCE(p_description, m.description)
     WHERE m.agent_id = p_agent_id
       AND m.pkid = p_pkid
    RETURNING m.pkid, m.title;
END;
$proc$;
