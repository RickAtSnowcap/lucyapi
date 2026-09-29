-- fn_admin_dashboard_stats.sql
-- Counts for the admin dashboard: owned + shared-to-me where sharing applies.

CREATE OR REPLACE FUNCTION lucyapi.fn_admin_dashboard_stats(p_user_id INT)
RETURNS TABLE(agents BIGINT, projects BIGINT, wikis BIGINT, hint_categories BIGINT,
              secrets BIGINT, images BIGINT, pending_handoffs BIGINT, shared_to_me BIGINT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT count(*) FROM public.agents a WHERE a.user_id = p_user_id),
        (SELECT count(*) FROM (
             SELECT p.project_id FROM public.projects p WHERE p.user_id = p_user_id
             UNION
             SELECT so.object_id FROM public.shared_objects so WHERE so.shared_to_user_id = p_user_id AND so.object_type_id = 1) t),
        (SELECT count(*) FROM (
             SELECT w.wiki_id FROM public.wikis w WHERE w.user_id = p_user_id
             UNION
             SELECT so.object_id FROM public.shared_objects so WHERE so.shared_to_user_id = p_user_id AND so.object_type_id = 3) t),
        (SELECT count(*) FROM (
             SELECT DISTINCT h.hint_category_id FROM public.hints h WHERE h.user_id = p_user_id AND h.parent_id = 0
             UNION
             SELECT so.object_id FROM public.shared_objects so WHERE so.shared_to_user_id = p_user_id AND so.object_type_id = 2) t),
        (SELECT count(*) FROM public.secrets s WHERE s.user_id = p_user_id),
        (SELECT count(*) FROM public.images i WHERE i.user_id = p_user_id),
        (SELECT count(*) FROM public.handoffs ho
          WHERE ho.agent_id IN (SELECT a.agent_id FROM public.agents a WHERE a.user_id = p_user_id)
            AND ho.picked_up_at IS NULL),
        (SELECT count(*) FROM public.shared_objects so WHERE so.shared_to_user_id = p_user_id);
END;
$proc$;
