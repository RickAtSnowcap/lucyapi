-- fn_always_load_update.sql
-- Updates an always_load node using COALESCE to preserve fields not provided.
-- Sets updated_at = NOW() on every update.

CREATE OR REPLACE FUNCTION lucyapi.fn_always_load_update(p_agent_id INT, p_pkid INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(pkid INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.always_load a
       SET title       = COALESCE(p_title, a.title),
           description = COALESCE(p_description, a.description),
           updated_at  = NOW()
     WHERE a.agent_id = p_agent_id
       AND a.pkid = p_pkid
    RETURNING a.pkid, a.title;
END;
$proc$;
