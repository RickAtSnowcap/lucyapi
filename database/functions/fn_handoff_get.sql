-- fn_handoff_get.sql
-- Returns a single handoff, regardless of pickup state, to its recipient or its creator.
-- updated_at is the creator's last edit (NULL = never edited). from_agent is NULL when the creator is unknown (handoffs created before migration 015).

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_get(p_caller_agent_id INT, p_handoff_id INT)
RETURNS TABLE(handoff_id INT, title TEXT, prompt TEXT, created_at TIMESTAMPTZ, picked_up_at TIMESTAMPTZ,
              to_agent TEXT, from_agent TEXT, updated_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT h.handoff_id,
           h.title,
           h.prompt,
           h.created_at,
           h.picked_up_at,
           ta.name,
           fa.name,
           h.updated_at
      FROM public.handoffs h
      JOIN public.agents ta ON ta.agent_id = h.agent_id
      LEFT JOIN public.agents fa ON fa.agent_id = h.created_by_agent_id
     WHERE h.handoff_id = p_handoff_id
       AND (h.agent_id = p_caller_agent_id OR h.created_by_agent_id = p_caller_agent_id);
END;
$proc$;
