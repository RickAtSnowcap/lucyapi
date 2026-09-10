-- fn_handoff_delete.sql
-- Deletes a handoff by agent_id and handoff_id. Returns count of deleted rows.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_delete(p_agent_id INT, p_handoff_id INT)
RETURNS TABLE(deleted_count INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM public.handoffs h
     WHERE h.agent_id = p_agent_id
       AND h.handoff_id = p_handoff_id;

    GET DIAGNOSTICS v_count = ROW_COUNT;

    RETURN QUERY SELECT v_count;
END;
$proc$;
