-- fn_handoff_update.sql
-- The creator edits a pending handoff's title and/or prompt (NULL = keep). Returns one row whose status says what
-- happened, so the caller can explain a refusal:
--   updated      the edit was saved
--   not_found    no such handoff, or the caller is neither its recipient nor its creator
--   not_creator  the caller is the recipient but didn't create it (from_agent names the creator; NULL = unknown)
--   picked_up    the caller created it, but the recipient already picked it up (picked_up_at says when)
-- The row lock makes the check and the edit atomic with respect to a concurrent pickup.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_update(p_caller_agent_id INT, p_handoff_id INT, p_title TEXT, p_prompt TEXT)
RETURNS TABLE(status TEXT, handoff_id INT, title TEXT, to_agent TEXT, from_agent TEXT, picked_up_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_agent_id   INT;
    v_created_by INT;
    v_picked_up  TIMESTAMPTZ;
    v_title      TEXT;
BEGIN
    SELECT h.agent_id, h.created_by_agent_id, h.picked_up_at, h.title
      INTO v_agent_id, v_created_by, v_picked_up, v_title
      FROM public.handoffs h
     WHERE h.handoff_id = p_handoff_id
       AND (h.agent_id = p_caller_agent_id OR h.created_by_agent_id = p_caller_agent_id)
       FOR UPDATE;

    IF NOT FOUND THEN
        RETURN QUERY SELECT 'not_found'::TEXT, p_handoff_id, NULL::TEXT, NULL::TEXT, NULL::TEXT, NULL::TIMESTAMPTZ;
        RETURN;
    END IF;

    IF v_created_by IS DISTINCT FROM p_caller_agent_id THEN
        RETURN QUERY SELECT 'not_creator'::TEXT, p_handoff_id, v_title,
               (SELECT a.name FROM public.agents a WHERE a.agent_id = v_agent_id),
               (SELECT a.name FROM public.agents a WHERE a.agent_id = v_created_by), v_picked_up;
        RETURN;
    END IF;

    IF v_picked_up IS NOT NULL THEN
        RETURN QUERY SELECT 'picked_up'::TEXT, p_handoff_id, v_title,
               (SELECT a.name FROM public.agents a WHERE a.agent_id = v_agent_id),
               (SELECT a.name FROM public.agents a WHERE a.agent_id = v_created_by), v_picked_up;
        RETURN;
    END IF;

    UPDATE public.handoffs h
       SET title  = COALESCE(p_title, h.title),
           prompt = COALESCE(p_prompt, h.prompt)
     WHERE h.handoff_id = p_handoff_id
    RETURNING h.title INTO v_title;

    RETURN QUERY SELECT 'updated'::TEXT, p_handoff_id, v_title,
           (SELECT a.name FROM public.agents a WHERE a.agent_id = v_agent_id),
           (SELECT a.name FROM public.agents a WHERE a.agent_id = v_created_by), NULL::TIMESTAMPTZ;
END;
$proc$;
