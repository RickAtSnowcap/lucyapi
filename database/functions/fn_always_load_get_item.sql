-- fn_always_load_get_item.sql
-- Returns an always_load node and its direct children.
-- is_child = false for the requested node, true for children.

CREATE OR REPLACE FUNCTION lucyapi.fn_always_load_get_item(p_agent_id INT, p_pkid INT)
RETURNS TABLE(pkid INT, parent_id INT, title TEXT, description TEXT, is_child BOOLEAN)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    -- The node itself
    SELECT a.pkid,
           a.parent_id,
           a.title,
           a.description,
           FALSE AS is_child
      FROM public.always_load a
     WHERE a.agent_id = p_agent_id
       AND a.pkid = p_pkid

    UNION ALL

    -- Direct children
    SELECT a.pkid,
           a.parent_id,
           a.title,
           a.description,
           TRUE AS is_child
      FROM public.always_load a
     WHERE a.agent_id = p_agent_id
       AND a.parent_id = p_pkid
     ORDER BY is_child, pkid;
END;
$proc$;
