-- 010-image-uploads.sql
-- Image features (LucyAPI project #3 §450): upload_image MCP tool, LucyAdmin upload, real file types.
-- images += title, description, mime_type, source (generated|edited|uploaded), agent_id (nullable; the
-- agent that created it, from the OAuth token — NULL for LucyAdmin uploads).
-- fn_image_insert takes the new columns (and keep); insert/list/get/update_keep return them.
-- Their return types change, so the old definitions are dropped first and re-created from functions/*.sql.
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction, then deploy the matching binary:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/010-image-uploads.sql \
--       functions/fn_image_{insert,list,get,update_keep}.sql; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_image_insert(INTEGER, INTEGER, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, BOOLEAN, INTEGER, INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_image_list(INTEGER, BOOLEAN, INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_image_get(INTEGER, INTEGER) TO leaddev;'; \
--       echo 'GRANT EXECUTE ON FUNCTION lucyapi.fn_image_update_keep(INTEGER, INTEGER, BOOLEAN) TO leaddev;'; \
--       echo 'COMMIT;' ) | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1
-- Then backfill rows created before 010 (mime type/size from the real file bytes; idempotent):
--   python3 tools/backfill_image_metadata.py
-- Applied to production 2026-10-01 (15 rows backfilled).

ALTER TABLE public.images
    ADD COLUMN IF NOT EXISTS title       TEXT,
    ADD COLUMN IF NOT EXISTS description TEXT,
    ADD COLUMN IF NOT EXISTS mime_type   TEXT,
    ADD COLUMN IF NOT EXISTS source      TEXT CHECK (source IN ('generated', 'edited', 'uploaded')),
    ADD COLUMN IF NOT EXISTS agent_id    INT REFERENCES public.agents(agent_id) ON DELETE SET NULL;

DROP FUNCTION IF EXISTS lucyapi.fn_image_insert(p_user_id integer, p_filename text, p_prompt text, p_model text, p_size_bytes integer, p_width integer, p_height integer);
DROP FUNCTION IF EXISTS lucyapi.fn_image_list(p_user_id integer, p_keep boolean, p_limit integer, p_offset integer);
DROP FUNCTION IF EXISTS lucyapi.fn_image_get(p_user_id integer, p_image_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_image_update_keep(p_user_id integer, p_image_id integer, p_keep boolean);
