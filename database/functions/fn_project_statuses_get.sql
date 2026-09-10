-- fn_project_statuses_get.sql
-- Returns all project statuses ordered by sort_order.

CREATE OR REPLACE FUNCTION lucyapi.fn_project_statuses_get()
RETURNS TABLE(status_id INT, code TEXT, label TEXT, sort_order INT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT ps.status_id,
           ps.code::TEXT,
           ps.label::TEXT,
           ps.sort_order
      FROM public.project_statuses ps
     ORDER BY ps.sort_order;
END;
$proc$;
