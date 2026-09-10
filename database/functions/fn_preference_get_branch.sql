-- fn_preference_get_branch.sql
-- Returns a preference node and its direct children.
-- is_child = false for the requested node, true for children.

CREATE OR REPLACE FUNCTION lucyapi.fn_preference_get_branch(p_agent_id INT, p_pkid INT)
RETURNS TABLE(pkid INT, parent_id INT, title TEXT, description TEXT, is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- The node itself
    SELECT p.pkid,
           p.parent_id,
           p.title,
           p.description,
           FALSE AS is_child
      FROM public.preferences p
     WHERE p.agent_id = p_agent_id
       AND p.pkid = p_pkid

    UNION ALL

    -- Direct children
    SELECT p.pkid,
           p.parent_id,
           p.title,
           p.description,
           TRUE AS is_child
      FROM public.preferences p
     WHERE p.agent_id = p_agent_id
       AND p.parent_id = p_pkid
     ORDER BY is_child, pkid;
END;
$proc$;
