-- fn_handoff_list_pending.sql
-- Returns pending (not yet picked up) handoffs addressed to an agent, FIFO, with the sender's name
-- (NULL when unknown: handoffs created before migration 015).

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_list_pending(p_agent_id INT)
RETURNS TABLE(handoff_id INT, title TEXT, prompt TEXT, created_at TIMESTAMPTZ, from_agent TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT h.handoff_id,
           h.title,
           h.prompt,
           h.created_at,
           fa.name
      FROM public.handoffs h
      LEFT JOIN public.agents fa ON fa.agent_id = h.created_by_agent_id
     WHERE h.agent_id = p_agent_id
       AND h.picked_up_at IS NULL
     ORDER BY h.created_at;
END;
$proc$;
