-- fn_oauth_revoke_agent.sql
-- Revokes every live OAuth token for an agent (lost phone, suspected compromise).
-- The connector must sign in again. Returns the number of tokens revoked.

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_revoke_agent(p_agent_id INTEGER)
RETURNS INTEGER
LANGUAGE plpgsql
AS $proc$
DECLARE
    n INTEGER;
BEGIN
    UPDATE public.oauth_tokens SET revoked_at = NOW()
     WHERE agent_id = p_agent_id AND revoked_at IS NULL;
    GET DIAGNOSTICS n = ROW_COUNT;
    RETURN n;
END;
$proc$;
