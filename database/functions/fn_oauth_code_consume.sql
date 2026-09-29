-- fn_oauth_code_consume.sql
-- Atomically redeems an authorization code: returns its bindings only if it exists, is unused,
-- and is unexpired, marking it used in the same statement (single-use even under a race).

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_code_consume(p_code_hash TEXT)
RETURNS TABLE(client_id TEXT, redirect_uri TEXT, code_challenge TEXT, resource TEXT,
              scope TEXT, user_id INTEGER, agent_id INTEGER)
LANGUAGE plpgsql
AS $proc$
BEGIN
    RETURN QUERY
    UPDATE public.oauth_auth_codes c
       SET used_at = NOW()
     WHERE c.code_hash = p_code_hash
       AND c.used_at IS NULL
       AND c.expires_at > NOW()
    RETURNING c.client_id, c.redirect_uri, c.code_challenge, c.resource, c.scope, c.user_id, c.agent_id;
END;
$proc$;
