-- fn_oauth_access_resolve.sql
-- Resolves a bearer access token to agent + user identity — the OAuth counterpart of
-- fn_agent_get_by_api_key. Valid only if unexpired, unrevoked, and issued for p_resource
-- (audience check). last_used_at is touched at most once a minute to avoid a write per call.

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_access_resolve(p_token_hash TEXT, p_resource TEXT)
RETURNS TABLE(agent_id INTEGER, agent_name TEXT, user_id INTEGER, user_name TEXT)
LANGUAGE plpgsql
AS $proc$
BEGIN
    UPDATE public.oauth_tokens ot
       SET last_used_at = NOW()
     WHERE ot.token_hash = p_token_hash
       AND ot.kind = 'access'
       AND (ot.last_used_at IS NULL OR ot.last_used_at < NOW() - INTERVAL '1 minute');

    RETURN QUERY
    SELECT a.agent_id, a.name AS agent_name, u.user_id, u.name AS user_name
      FROM public.oauth_tokens ot
      JOIN public.agents a ON a.agent_id = ot.agent_id
      JOIN public.users  u ON u.user_id  = ot.user_id
     WHERE ot.token_hash = p_token_hash
       AND ot.kind = 'access'
       AND ot.revoked_at IS NULL
       AND ot.expires_at > NOW()
       AND ot.resource = p_resource;
END;
$proc$;
