-- fn_memory_get_all.sql
-- Returns all memories for an agent, ordered by pkid.

CREATE OR REPLACE FUNCTION lucyapi.fn_memory_get_all(p_agent_id INT)
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
     ORDER BY m.pkid;
END;
$proc$;
