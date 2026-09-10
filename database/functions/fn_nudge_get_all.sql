-- fn_nudge_get_all.sql
-- Returns all nudges for a user, ASAP (NULL due_date) first, then by due_date ASC.

CREATE OR REPLACE FUNCTION lucyapi.fn_nudge_get_all(p_user_id INT)
RETURNS TABLE(nudge_id INT, user_id INT, title TEXT, description TEXT, due_date DATE, last_reminded TIMESTAMPTZ, created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT n.nudge_id, n.user_id, n.title, n.description, n.due_date, n.last_reminded, n.created_at, n.updated_at
    FROM public.nudges n
    WHERE n.user_id = p_user_id
    ORDER BY n.due_date NULLS FIRST, n.due_date ASC;
END;
$proc$;
