-- fn_preference_create.sql
-- Inserts a new preference and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_preference_create(p_agent_id INT, p_parent_id INT, p_title TEXT, p_description TEXT)
RETURNS TABLE(pkid INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.preferences (agent_id, parent_id, title, description)
    VALUES (p_agent_id, p_parent_id, p_title, p_description)
    RETURNING preferences.pkid, preferences.title;
END;
$proc$;
