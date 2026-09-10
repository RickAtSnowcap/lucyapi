-- fn_preference_update.sql
-- Updates a preference using COALESCE to preserve fields not provided.
-- Sets updated_at = NOW() on every update.

CREATE OR REPLACE FUNCTION lucyapi.fn_preference_update(p_agent_id INT, p_pkid INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(pkid INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.preferences p
       SET title       = COALESCE(p_title, p.title),
           description = COALESCE(p_description, p.description),
           updated_at  = NOW()
     WHERE p.agent_id = p_agent_id
       AND p.pkid = p_pkid
    RETURNING p.pkid, p.title;
END;
$proc$;
