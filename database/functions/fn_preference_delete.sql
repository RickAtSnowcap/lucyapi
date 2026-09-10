-- fn_preference_delete.sql
-- Deletes a preference node and all descendants via recursive CTE.
-- Returns total count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_preference_delete(p_agent_id INT, p_pkid INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    WITH RECURSIVE subtree AS (
        SELECT p.pkid
          FROM public.preferences p
         WHERE p.agent_id = p_agent_id
           AND p.pkid = p_pkid

        UNION ALL

        SELECT p.pkid
          FROM public.preferences p
          JOIN subtree s ON s.pkid = p.parent_id
         WHERE p.agent_id = p_agent_id
    )
    DELETE FROM public.preferences
     WHERE pkid IN (SELECT pkid FROM subtree);

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
