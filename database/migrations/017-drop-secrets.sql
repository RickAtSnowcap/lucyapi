-- 017-drop-secrets.sql
-- Secrets store retirement (2026-10-27): LucyAPI no longer keeps per-user secrets. The get/set/list/delete_secret
-- MCP tools, the /admin/secrets endpoints and the LucyAdmin Secrets page are gone, and Google Docs reads its one
-- server-wide identity from server config (Suitcase:GoogleClientId / GoogleClientSecret / GoogleRefreshToken /
-- GoogleFolderId, plus GoogleDocs:OwnerUserId). Drops the four fn_secret_* functions, then the secrets table (its sequence,
-- primary key, unique constraint and index go with it).
-- fn_admin_dashboard_stats loses its secrets count (return type changes), so it is dropped here and re-created from
-- functions/fn_admin_dashboard_stats.sql.
--
-- ORDER MATTERS: put the Google settings in the config first, then STOP lucyapi, apply this, install the new binary
-- and start it (one stop window). The old binary calls fn_secret_* and the old dashboard return type.
--
-- Apply (objects owned by lucyapi_owner), in ONE transaction:
--   ( echo 'SET ROLE lucyapi_owner;'; echo 'BEGIN;'; cat migrations/017-drop-secrets.sql \
--       functions/fn_admin_dashboard_stats.sql; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_dashboard_stats(INTEGER) TO lucyapi_app;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_dashboard_stats(INTEGER) TO leaddev;'; \
--       echo 'COMMIT;' ) | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

DROP FUNCTION IF EXISTS lucyapi.fn_secret_list(p_user_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_secret_get(p_user_id integer, p_key text);
DROP FUNCTION IF EXISTS lucyapi.fn_secret_set(p_user_id integer, p_key text, p_encrypted_value bytea);
DROP FUNCTION IF EXISTS lucyapi.fn_secret_delete(p_user_id integer, p_key text);
DROP FUNCTION IF EXISTS lucyapi.fn_admin_dashboard_stats(p_user_id integer);

DROP TABLE IF EXISTS public.secrets;
