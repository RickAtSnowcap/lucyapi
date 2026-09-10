-- fn_always_load_delete.sql
-- Deletes an always_load node and all descendants via recursive CTE.
-- Returns total count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_always_load_delete(p_agent_id INT, p_pkid INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    WITH RECURSIVE subtree AS (
        SELECT a.pkid
          FROM public.always_load a
         WHERE a.agent_id = p_agent_id
           AND a.pkid = p_pkid

        UNION ALL

        SELECT a.pkid
          FROM public.always_load a
          JOIN subtree s ON s.pkid = a.parent_id
         WHERE a.agent_id = p_agent_id
    )
    DELETE FROM public.always_load
     WHERE pkid IN (SELECT pkid FROM subtree);

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
