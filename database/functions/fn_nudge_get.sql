-- fn_nudge_get.sql
-- Returns a single nudge by user_id and nudge_id.

CREATE OR REPLACE FUNCTION lucyapi.fn_nudge_get(p_user_id INT, p_nudge_id INT)
RETURNS TABLE(nudge_id INT, user_id INT, title TEXT, description TEXT, due_date DATE, last_reminded TIMESTAMPTZ, created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT n.nudge_id, n.user_id, n.title, n.description, n.due_date, n.last_reminded, n.created_at, n.updated_at
    FROM public.nudges n
    WHERE n.nudge_id = p_nudge_id
      AND n.user_id = p_user_id;
END;
$proc$;
