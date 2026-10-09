-- fn_handoff_create.sql
-- Creates a handoff. p_agent_id is the RECIPIENT; p_created_by is the sending agent (the caller).

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_create(p_agent_id INT, p_title TEXT, p_prompt TEXT, p_created_by INT)
RETURNS TABLE(handoff_id INT, title TEXT, created_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.handoffs (agent_id, title, prompt, created_by_agent_id)
    VALUES (p_agent_id, p_title, p_prompt, p_created_by)
    RETURNING handoffs.handoff_id, handoffs.title, handoffs.created_at;
END;
$proc$;
