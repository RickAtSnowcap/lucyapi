-- fn_preference_get_all.sql
-- Returns ALL preferences for an agent (full tree, flat list; tree built in the app layer).
-- Used by the admin preferences view. (Existed only in the live DB until 2026-09-29 — source restored.)

CREATE OR REPLACE FUNCTION lucyapi.fn_preference_get_all(p_agent_id INT)
RETURNS TABLE(pkid INT, parent_id INT, title TEXT, description TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT p.pkid,
           p.parent_id,
           p.title,
           p.description
      FROM public.preferences p
     WHERE p.agent_id = p_agent_id
     ORDER BY p.parent_id, p.pkid;
END;
$proc$;
