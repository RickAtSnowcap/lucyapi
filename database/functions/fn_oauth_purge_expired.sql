-- fn_oauth_purge_expired.sql
-- Housekeeping: deletes auth codes expired > 1 day ago and tokens expired > 7 days ago
-- (kept briefly so a replayed stale refresh token still hits reuse detection).

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_purge_expired()
RETURNS INTEGER
LANGUAGE plpgsql
AS $proc$
DECLARE
    n_codes  INTEGER;
    n_tokens INTEGER;
BEGIN
    DELETE FROM public.oauth_auth_codes WHERE expires_at < NOW() - INTERVAL '1 day';
    GET DIAGNOSTICS n_codes = ROW_COUNT;
    DELETE FROM public.oauth_tokens WHERE expires_at < NOW() - INTERVAL '7 days';
    GET DIAGNOSTICS n_tokens = ROW_COUNT;
    RETURN n_codes + n_tokens;
END;
$proc$;
