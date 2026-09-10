-- fn_agent_get_by_name.sql
-- Looks up an agent by name. Used for cross-agent handoff targeting.

CREATE OR REPLACE FUNCTION lucyapi.fn_agent_get_by_name(p_agent_name TEXT)
RETURNS TABLE(agent_id INTEGER, user_id INTEGER)
LANGUAGE plpgsql
AS $proc$
BEGIN
    RETURN QUERY
    SELECT a.agent_id,
           a.user_id
      FROM public.agents a
     WHERE a.name = p_agent_name;
END;
$proc$;
