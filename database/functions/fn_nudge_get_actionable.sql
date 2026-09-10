-- fn_nudge_get_actionable.sql
-- Returns nudges worth surfacing now based on frequency rules, and auto-stamps last_reminded.
-- ASAP (due_date IS NULL):       surface if never reminded or reminded > 24h ago.
-- Due in < 7 days:               surface if never reminded or reminded > 24h ago.
-- Due in >= 7 days:              surface if never reminded or reminded > 7 days ago.
-- Calling this function IS the reminder act -- no separate mark-as-reminded call needed.

CREATE OR REPLACE FUNCTION lucyapi.fn_nudge_get_actionable(p_user_id INT)
RETURNS TABLE(nudge_id INT, title TEXT, description TEXT, due_date DATE)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_ids INT[];
BEGIN
    -- Collect qualifying nudge IDs based on frequency rules.
    SELECT ARRAY(
        SELECT n.nudge_id
        FROM public.nudges n
        WHERE n.user_id = p_user_id
          AND (
              (n.due_date IS NULL
               AND (n.last_reminded IS NULL OR n.last_reminded < NOW() - INTERVAL '24 hours'))
              OR
              (n.due_date IS NOT NULL AND n.due_date - CURRENT_DATE < 7
               AND (n.last_reminded IS NULL OR n.last_reminded < NOW() - INTERVAL '24 hours'))
              OR
              (n.due_date IS NOT NULL AND n.due_date - CURRENT_DATE >= 7
               AND (n.last_reminded IS NULL OR n.last_reminded < NOW() - INTERVAL '7 days'))
          )
    ) INTO v_ids;

    -- Stamp last_reminded on all qualifying nudges.
    UPDATE public.nudges n
       SET last_reminded = NOW()
     WHERE n.nudge_id = ANY(v_ids);

    -- Return the qualifying nudges ordered ASAP first, then soonest due.
    RETURN QUERY
    SELECT n.nudge_id, n.title, n.description, n.due_date
    FROM public.nudges n
    WHERE n.nudge_id = ANY(v_ids)
    ORDER BY n.due_date NULLS FIRST, n.due_date ASC;
END;
$proc$;
