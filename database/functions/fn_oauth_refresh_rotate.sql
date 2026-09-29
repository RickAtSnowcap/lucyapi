-- fn_oauth_refresh_rotate.sql
-- Exchanges a refresh token for a new access + refresh pair in the same family.
-- status:
--   'ok'             — rotated; new pair stored (sliding window: new refresh gets p_refresh_expires)
--   'invalid'        — unknown, wrong client, expired, or revoked
--   'reuse_detected' — token was ALREADY rotated: treated as theft, whole family revoked

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_refresh_rotate(
    p_refresh_hash     TEXT,
    p_client_id        TEXT,
    p_new_access_hash  TEXT,
    p_new_refresh_hash TEXT,
    p_access_expires   TIMESTAMPTZ,
    p_refresh_expires  TIMESTAMPTZ)
RETURNS TABLE(status TEXT, agent_id INTEGER, user_id INTEGER, resource TEXT, scope TEXT)
LANGUAGE plpgsql
AS $proc$
DECLARE
    t public.oauth_tokens%ROWTYPE;
BEGIN
    SELECT * INTO t
      FROM public.oauth_tokens ot
     WHERE ot.token_hash = p_refresh_hash AND ot.kind = 'refresh'
       FOR UPDATE;

    IF NOT FOUND OR t.client_id <> p_client_id OR t.revoked_at IS NOT NULL THEN
        RETURN QUERY SELECT 'invalid'::TEXT, NULL::INTEGER, NULL::INTEGER, NULL::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    IF t.rotated_at IS NOT NULL THEN
        UPDATE public.oauth_tokens SET revoked_at = NOW()
         WHERE family_id = t.family_id AND revoked_at IS NULL;
        RETURN QUERY SELECT 'reuse_detected'::TEXT, t.agent_id, t.user_id, NULL::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    IF t.expires_at <= NOW() THEN
        RETURN QUERY SELECT 'invalid'::TEXT, NULL::INTEGER, NULL::INTEGER, NULL::TEXT, NULL::TEXT;
        RETURN;
    END IF;

    UPDATE public.oauth_tokens SET rotated_at = NOW(), last_used_at = NOW() WHERE token_id = t.token_id;

    INSERT INTO public.oauth_tokens
           (token_hash, kind, family_id, client_id, user_id, agent_id, resource, scope, expires_at)
    VALUES (p_new_access_hash,  'access',  t.family_id, t.client_id, t.user_id, t.agent_id, t.resource, t.scope, p_access_expires),
           (p_new_refresh_hash, 'refresh', t.family_id, t.client_id, t.user_id, t.agent_id, t.resource, t.scope, p_refresh_expires);

    UPDATE public.oauth_clients SET last_used_at = NOW() WHERE client_id = t.client_id;

    RETURN QUERY SELECT 'ok'::TEXT, t.agent_id, t.user_id, t.resource, t.scope;
END;
$proc$;
