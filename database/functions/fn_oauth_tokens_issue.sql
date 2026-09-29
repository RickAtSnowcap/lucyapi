-- fn_oauth_tokens_issue.sql
-- Stores a new access + refresh token pair (hashes only) in a token family.

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_tokens_issue(
    p_family_id        UUID,
    p_access_hash      TEXT,
    p_refresh_hash     TEXT,
    p_client_id        TEXT,
    p_user_id          INTEGER,
    p_agent_id         INTEGER,
    p_resource         TEXT,
    p_scope            TEXT,
    p_access_expires   TIMESTAMPTZ,
    p_refresh_expires  TIMESTAMPTZ)
RETURNS VOID
LANGUAGE plpgsql
AS $proc$
BEGIN
    INSERT INTO public.oauth_tokens
           (token_hash, kind, family_id, client_id, user_id, agent_id, resource, scope, expires_at)
    VALUES (p_access_hash,  'access',  p_family_id, p_client_id, p_user_id, p_agent_id, p_resource, p_scope, p_access_expires),
           (p_refresh_hash, 'refresh', p_family_id, p_client_id, p_user_id, p_agent_id, p_resource, p_scope, p_refresh_expires);

    UPDATE public.oauth_clients SET last_used_at = NOW() WHERE client_id = p_client_id;
END;
$proc$;
