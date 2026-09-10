-- fn_memory_delete.sql
-- Deletes a memory by agent_id and pkid. Returns count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_memory_delete(p_agent_id INT, p_pkid INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.memories m
     WHERE m.agent_id = p_agent_id
       AND m.pkid = p_pkid;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
