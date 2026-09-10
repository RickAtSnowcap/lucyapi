-- fn_handoff_list_pending.sql
-- Returns pending (not yet picked up) handoffs for an agent. FIFO order.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_list_pending(p_agent_id INT)
RETURNS TABLE(handoff_id INT, title TEXT, prompt TEXT, created_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT h.handoff_id,
           h.title,
           h.prompt,
           h.created_at
      FROM public.handoffs h
     WHERE h.agent_id = p_agent_id
       AND h.picked_up_at IS NULL
     ORDER BY h.created_at;
END;
$proc$;
