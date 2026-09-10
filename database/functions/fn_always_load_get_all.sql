-- fn_always_load_get_all.sql
-- Returns all always_load nodes for an agent as a flat list.
-- Tree building happens in the application layer.

CREATE OR REPLACE FUNCTION lucyapi.fn_always_load_get_all(p_agent_id INT)
RETURNS TABLE(pkid INT, parent_id INT, title TEXT, description TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT a.pkid,
           a.parent_id,
           a.title,
           a.description
      FROM public.always_load a
     WHERE a.agent_id = p_agent_id
     ORDER BY a.parent_id, a.pkid;
END;
$proc$;
