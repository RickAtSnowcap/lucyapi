-- fn_always_load_create.sql
-- Inserts a new always_load node and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_always_load_create(p_agent_id INT, p_parent_id INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(pkid INT, parent_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.always_load (agent_id, parent_id, title, description)
    VALUES (p_agent_id, p_parent_id, p_title, p_description)
    RETURNING always_load.pkid, always_load.parent_id, always_load.title;
END;
$proc$;
