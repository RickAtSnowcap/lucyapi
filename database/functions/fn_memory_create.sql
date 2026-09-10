-- fn_memory_create.sql
-- Inserts a new memory and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_memory_create(p_agent_id INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(pkid INT, title TEXT, created_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.memories (agent_id, title, description)
    VALUES (p_agent_id, p_title, p_description)
    RETURNING memories.pkid, memories.title, memories.created_at;
END;
$proc$;
