-- fn_handoff_delete.sql
-- Deletes a handoff for its recipient (any time) or its creator (only while pending). Returns one row whose status
-- says what happened:
--   deleted    the handoff is gone
--   not_found  no such handoff, or the caller is neither its recipient nor its creator
--   picked_up  the caller created it (and isn't the recipient), but it was already picked up; only the recipient
--              may delete it now
-- The row lock makes the check and the delete atomic with respect to a concurrent pickup.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_delete(p_caller_agent_id INT, p_handoff_id INT)
RETURNS TABLE(status TEXT, handoff_id INT, title TEXT, to_agent TEXT, picked_up_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_agent_id  INT;
    v_picked_up TIMESTAMPTZ;
    v_title     TEXT;
    v_to        TEXT;
BEGIN
    SELECT h.agent_id, h.picked_up_at, h.title
      INTO v_agent_id, v_picked_up, v_title
      FROM public.handoffs h
     WHERE h.handoff_id = p_handoff_id
       AND (h.agent_id = p_caller_agent_id OR h.created_by_agent_id = p_caller_agent_id)
       FOR UPDATE;

    IF NOT FOUND THEN
        RETURN QUERY SELECT 'not_found'::TEXT, p_handoff_id, NULL::TEXT, NULL::TEXT, NULL::TIMESTAMPTZ;
        RETURN;
    END IF;

    SELECT a.name INTO v_to FROM public.agents a WHERE a.agent_id = v_agent_id;

    IF v_agent_id <> p_caller_agent_id AND v_picked_up IS NOT NULL THEN
        RETURN QUERY SELECT 'picked_up'::TEXT, p_handoff_id, v_title, v_to, v_picked_up;
        RETURN;
    END IF;

    DELETE FROM public.handoffs h WHERE h.handoff_id = p_handoff_id;

    RETURN QUERY SELECT 'deleted'::TEXT, p_handoff_id, v_title, v_to, v_picked_up;
END;
$proc$;
