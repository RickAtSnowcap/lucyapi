-- fn_memory_get_one.sql
-- Returns a single memory by agent_id and pkid.

CREATE OR REPLACE FUNCTION lucyapi.fn_memory_get_one(p_agent_id INT, p_pkid INT)
RETURNS TABLE(pkid INT, title TEXT, description TEXT, created_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT m.pkid,
           m.title,
           m.description,
           m.created_at
      FROM public.memories m
     WHERE m.agent_id = p_agent_id
       AND m.pkid = p_pkid;
END;
$proc$;
