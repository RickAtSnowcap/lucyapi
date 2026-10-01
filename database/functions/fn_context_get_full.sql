-- fn_context_get_full.sql
-- Returns the full agent context as a single JSONB object.
-- One-stop-shop for agent startup: time, always_load (titles only),
-- memory titles, preference manifest, project manifest (with status code),
-- hints compact (flat list), actionable nudges, and the new session.
-- Side effects (so they run into variables before the main JSONB build, not inside subqueries):
--   * fn_nudge_get_actionable stamps nudges.last_reminded;
--   * every call opens a new session for p_agent_id (the agent whose context this is) and reports the
--     previous session's start (gap detection). Projects loaded later attach via fn_session_add_project.

CREATE OR REPLACE FUNCTION lucyapi.fn_context_get_full(p_agent_id INT, p_user_id INT)
RETURNS TABLE(context_json JSONB)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_nudges JSONB;
    v_mountain TIMESTAMPTZ;
    v_is_dst BOOLEAN;
    v_previous_started TIMESTAMPTZ;
    v_session_id INT;
    v_session_started TIMESTAMPTZ;
BEGIN
    SELECT max(s.started_at) INTO v_previous_started
      FROM public.sessions s
     WHERE s.agent_id = p_agent_id;

    INSERT INTO public.sessions (agent_id)
    VALUES (p_agent_id)
    RETURNING sessions.session_id, sessions.started_at INTO v_session_id, v_session_started;

    -- Compute Mountain Time for the time section.
    v_mountain := now() AT TIME ZONE 'America/Denver';
    -- MDT = UTC-6 (-21600s), MST = UTC-7 (-25200s). Compare current Denver offset to MST.
    v_is_dst := (now() AT TIME ZONE 'America/Denver') != (now() AT TIME ZONE 'MST');

    -- Call fn_nudge_get_actionable first; its side-effect UPDATE stamps last_reminded.
    SELECT COALESCE(
        jsonb_agg(jsonb_build_object(
            'nudge_id',    n.nudge_id,
            'title',       n.title,
            'description', n.description,
            'due_date',    n.due_date
        )),
        '[]'::jsonb
    )
    INTO v_nudges
    FROM lucyapi.fn_nudge_get_actionable(p_user_id) n;

    RETURN QUERY
    SELECT jsonb_build_object(
        'time', jsonb_build_object(
            'utc_time',       to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS'),
            'mountain_time',  to_char(v_mountain, 'YYYY-MM-DD HH24:MI:SS'),
            'timezone',       CASE WHEN v_is_dst THEN 'MDT' ELSE 'MST' END,
            'day_of_week',    TRIM(to_char(v_mountain, 'Day'))
        ),

        'always_load',
        COALESCE(
            (SELECT jsonb_agg(jsonb_build_object(
                'pkid', al.pkid,
                'parent_id', al.parent_id,
                'title', al.title
            ) ORDER BY al.parent_id, al.pkid)
            FROM public.always_load al
            WHERE al.agent_id = p_agent_id),
            '[]'::jsonb
        ),

        'memories',
        COALESCE(
            (SELECT jsonb_agg(jsonb_build_object(
                'pkid', m.pkid,
                'title', m.title
            ) ORDER BY m.pkid)
            FROM public.memories m
            WHERE m.agent_id = p_agent_id),
            '[]'::jsonb
        ),

        'preferences_manifest',
        COALESCE(
            (SELECT jsonb_agg(jsonb_build_object(
                'pkid', p.pkid,
                'title', p.title
            ) ORDER BY p.pkid)
            FROM public.preferences p
            WHERE p.agent_id = p_agent_id
              AND p.parent_id = 0),
            '[]'::jsonb
        ),

        'projects_manifest',
        COALESCE(
            (SELECT jsonb_agg(jsonb_build_object(
                'project_id', pr.project_id,
                'title', pr.title,
                'status', ps.code
            ) ORDER BY pr.project_id)
            FROM public.projects pr
            JOIN public.project_statuses ps ON pr.status_id = ps.status_id
            WHERE pr.user_id = p_user_id),
            '[]'::jsonb
        ),

        'hints_compact',
        COALESCE(
            (SELECT jsonb_agg(jsonb_build_object(
                'pkid',       hc.hint_id,
                'parent_id',  hc.parent_id,
                'title',      hc.title,
                'sort_order', hc.sort_order
            ) ORDER BY hc.parent_id, hc.sort_order, hc.hint_id)
            FROM lucyapi.fn_hint_get_compact(p_user_id) hc),
            '[]'::jsonb
        ),

        'actionable_nudges', v_nudges,

        'session', jsonb_build_object(
            'session_id',          v_session_id,
            'started_at',          to_char(v_session_started AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS'),
            'previous_started_at', to_char(v_previous_started AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS'),
            'previous_started_mountain', to_char(v_previous_started AT TIME ZONE 'America/Denver', 'YYYY-MM-DD HH24:MI:SS')
        )
    ) AS context_json;
END;
$proc$;
