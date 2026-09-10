-- fn_preference_get_top_level.sql
-- Returns root-level preferences (parent_id = 0) with titles only.
-- Used as the "manifest" view for the /context endpoint.

CREATE OR REPLACE FUNCTION lucyapi.fn_preference_get_top_level(p_agent_id INT)
RETURNS TABLE(pkid INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT p.pkid,
           p.title
      FROM public.preferences p
     WHERE p.agent_id = p_agent_id
       AND p.parent_id = 0
     ORDER BY p.pkid;
END;
$proc$;
