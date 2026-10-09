-- fn_handoff_list_sent.sql
-- Handoffs an agent created (sent), newest first. Returns up to 101 rows: the API shows 100 and reports
-- "truncated" when the 101st exists. p_pending_only: only those not yet picked up.
-- No prompt text (get the handoff for that).

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_list_sent(p_caller_agent_id INT, p_pending_only BOOLEAN)
RETURNS TABLE(handoff_id INT, title TEXT, created_at TIMESTAMPTZ, picked_up_at TIMESTAMPTZ, to_agent TEXT,
              updated_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT h.handoff_id,
           h.title,
           h.created_at,
           h.picked_up_at,
           ta.name,
           h.updated_at
      FROM public.handoffs h
      JOIN public.agents ta ON ta.agent_id = h.agent_id
     WHERE h.created_by_agent_id = p_caller_agent_id
       AND (NOT p_pending_only OR h.picked_up_at IS NULL)
     ORDER BY h.created_at DESC, h.handoff_id DESC
     LIMIT 101;
END;
$proc$;
