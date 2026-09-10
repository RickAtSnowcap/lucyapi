-- fn_nudge_update.sql
-- Updates a nudge. COALESCE preserves title/description if not provided.
-- due_date is always overwritten (NULL means ASAP; the API layer controls whether to pass NULL).

CREATE OR REPLACE FUNCTION lucyapi.fn_nudge_update(p_user_id INT, p_nudge_id INT, p_title TEXT, p_description TEXT, p_due_date DATE)
RETURNS TABLE(nudge_id INT, title TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.nudges n
       SET title       = COALESCE(p_title, n.title),
           description = COALESCE(p_description, n.description),
           due_date    = p_due_date,
           updated_at  = NOW()
     WHERE n.nudge_id = p_nudge_id
       AND n.user_id = p_user_id
    RETURNING n.nudge_id, n.title;
END;
$proc$;
