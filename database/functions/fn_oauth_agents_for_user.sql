-- fn_oauth_agents_for_user.sql
-- Agents a signed-in user may bind a connector to (the consent page's agent chooser).

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_agents_for_user(p_user_id INTEGER)
RETURNS TABLE(agent_id INTEGER, agent_name TEXT)
LANGUAGE plpgsql
AS $proc$
BEGIN
    RETURN QUERY
    SELECT a.agent_id, a.name
      FROM public.agents a
     WHERE a.user_id = p_user_id
     ORDER BY a.name;
END;
$proc$;
