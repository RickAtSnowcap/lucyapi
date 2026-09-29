-- fn_oauth_client_get.sql
-- Looks up a registered (DCR) or cached (CIMD) OAuth client.

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_client_get(p_client_id TEXT)
RETURNS TABLE(client_id TEXT, registration_type TEXT, client_name TEXT,
              redirect_uris TEXT[], metadata_fetched_at TIMESTAMPTZ)
LANGUAGE plpgsql
AS $proc$
BEGIN
    RETURN QUERY
    SELECT c.client_id, c.registration_type, c.client_name, c.redirect_uris, c.metadata_fetched_at
      FROM public.oauth_clients c
     WHERE c.client_id = p_client_id;
END;
$proc$;
