-- fn_oauth_code_create.sql
-- Stores a new authorization code (hash only). The agent must belong to the user.

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_code_create(
    p_code_hash      TEXT,
    p_client_id      TEXT,
    p_redirect_uri   TEXT,
    p_code_challenge TEXT,
    p_resource       TEXT,
    p_scope          TEXT,
    p_user_id        INTEGER,
    p_agent_id       INTEGER,
    p_expires_at     TIMESTAMPTZ)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $proc$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM public.agents a WHERE a.agent_id = p_agent_id AND a.user_id = p_user_id) THEN
        RETURN FALSE;
    END IF;

    INSERT INTO public.oauth_auth_codes
           (code_hash, client_id, redirect_uri, code_challenge, resource, scope, user_id, agent_id, expires_at)
    VALUES (p_code_hash, p_client_id, p_redirect_uri, p_code_challenge, p_resource, p_scope, p_user_id, p_agent_id, p_expires_at);
    RETURN TRUE;
END;
$proc$;
