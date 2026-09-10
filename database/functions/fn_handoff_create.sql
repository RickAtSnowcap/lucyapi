-- fn_handoff_create.sql
-- Creates a handoff targeting an agent. p_agent_id is the TARGET agent.

CREATE OR REPLACE FUNCTION lucyapi.fn_handoff_create(p_agent_id INT, p_title TEXT, p_prompt TEXT)
RETURNS TABLE(handoff_id INT, title TEXT, created_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.handoffs (agent_id, title, prompt)
    VALUES (p_agent_id, p_title, p_prompt)
    RETURNING handoffs.handoff_id, handoffs.title, handoffs.created_at;
END;
$proc$;
