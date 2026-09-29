-- 008-drop-first-key-fn.sql
-- Drops lucyapi.fn_agent_get_first_key_by_user_id: it returned a user's plaintext agent key, existed only in
-- the live DB (never in database/functions/), and nothing calls it since 6a638aa (signed document links).
-- Also re-applies fn_preference_get_all from its restored source file (identical definition, now under source control).
--
-- Apply:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/008-drop-first-key-fn.sql functions/fn_preference_get_all.sql; echo 'COMMIT;' ) \
--     | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

DROP FUNCTION IF EXISTS lucyapi.fn_agent_get_first_key_by_user_id(p_user_id integer);
