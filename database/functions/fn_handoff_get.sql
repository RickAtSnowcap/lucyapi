-- fn_handoff_get.sql
-- Returns a single handoff regardless of pickup state.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_get(p_agent_id INT, p_handoff_id INT)
RETURNS TABLE(handoff_id INT, title TEXT, prompt TEXT, created_at TIMESTAMPTZ, picked_up_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT h.handoff_id,
           h.title,
           h.prompt,
           h.created_at,
           h.picked_up_at
      FROM public.handoffs h
     WHERE h.agent_id = p_agent_id
       AND h.handoff_id = p_handoff_id;
END;
$proc$;
