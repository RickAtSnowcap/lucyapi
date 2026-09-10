-- fn_nudge_create.sql
-- Inserts a new nudge and returns the created row.

CREATE OR REPLACE FUNCTION lucyapi.fn_nudge_create(p_user_id INT, p_title TEXT, p_description TEXT, p_due_date DATE)
RETURNS TABLE(nudge_id INT, title TEXT, created_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    INSERT INTO public.nudges (user_id, title, description, due_date)
    VALUES (p_user_id, p_title, p_description, p_due_date)
    RETURNING nudges.nudge_id, nudges.title, nudges.created_at;
END;
$proc$;
