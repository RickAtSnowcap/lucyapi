-- fn_oauth_client_upsert.sql
-- Registers a DCR client, or caches/refreshes a CIMD client's fetched metadata.

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_client_upsert(
    p_client_id         TEXT,
    p_registration_type TEXT,
    p_client_name       TEXT,
    p_redirect_uris     TEXT[])
RETURNS VOID
LANGUAGE plpgsql
AS $proc$
BEGIN
    INSERT INTO public.oauth_clients (client_id, registration_type, client_name, redirect_uris, metadata_fetched_at)
    VALUES (p_client_id, p_registration_type, p_client_name, p_redirect_uris,
            CASE WHEN p_registration_type = 'cimd' THEN NOW() END)
    ON CONFLICT (client_id) DO UPDATE
       SET client_name         = EXCLUDED.client_name,
           redirect_uris       = EXCLUDED.redirect_uris,
           metadata_fetched_at = EXCLUDED.metadata_fetched_at
     WHERE public.oauth_clients.registration_type = EXCLUDED.registration_type;
END;
$proc$;
