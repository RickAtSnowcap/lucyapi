-- fn_agent_get_by_api_key.sql
-- Resolves an API key to agent + user identity. Called on every authenticated request.

CREATE OR REPLACE FUNCTION lucyapi.fn_agent_get_by_api_key(p_api_key TEXT)
RETURNS TABLE(agent_id INTEGER, agent_name TEXT, user_id INTEGER, user_name TEXT)
LANGUAGE plpgsql
AS $proc$
BEGIN
    RETURN QUERY
    SELECT a.agent_id,
           a.name       AS agent_name,
           u.user_id,
           u.name       AS user_name
      FROM public.agents a
      JOIN public.users  u ON u.user_id = a.user_id
     WHERE a.api_key = p_api_key;
END;
$proc$;
