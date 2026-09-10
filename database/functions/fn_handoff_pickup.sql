-- fn_handoff_pickup.sql
-- Marks a pending handoff as picked up. Only works if picked_up_at IS NULL.
-- Returns empty result if already picked up or not found.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_pickup(p_agent_id INT, p_handoff_id INT)
RETURNS TABLE(handoff_id INT, title TEXT, picked_up_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.handoffs h
       SET picked_up_at = NOW()
     WHERE h.agent_id = p_agent_id
       AND h.handoff_id = p_handoff_id
       AND h.picked_up_at IS NULL
    RETURNING h.handoff_id, h.title, h.picked_up_at;
END;
$proc$;
