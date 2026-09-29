-- 007-ownership.sql
-- Ownership / sharing enforced INSIDE the database (LucyAPI project #62 follow-up, 2026-09-29).
-- Every function that acts on a project, section, wiki, wiki section, hint or image by ID now takes the
-- caller's user (p_user_id, FIRST parameter, always from the authenticated context — never request input)
-- and checks lucyapi.fn_access_level: read >= 1; create/edit/delete of sub-entries >= 2;
-- delete of the main entry (project, wiki, root hint category) and sharing = 3 (owner or admin share).
-- Images are owner-only. Denied = "not found" (no row / 0), so IDs of other users' objects don't leak.
--
-- This file drops the old signatures (incl. stale overloads of fn_hint_update / fn_hint_category_create);
-- the new definitions live in functions/*.sql.
--
-- Apply (objects owned by leaddev — hint #189), in ONE transaction, then deploy the matching binary:
--   ( echo 'SET ROLE leaddev;'; echo 'BEGIN;'; cat migrations/007-ownership.sql functions/fn_access_level.sql \
--       functions/fn_project_{update,delete,get_sections}.sql functions/fn_section_{get_all_compact,get,create,update,delete}.sql \
--       functions/fn_wiki_{update,delete,get_sections,tags_get}.sql functions/fn_wiki_section_{get,create,update,delete}.sql \
--       functions/fn_hint_{get,create,update,delete,category_create,category_delete}.sql \
--       functions/fn_image_{get,update_keep,delete,delete_batch}.sql functions/fn_share_create.sql; echo 'COMMIT;' ) \
--     | sudo -u postgres psql -d lucyapi -v ON_ERROR_STOP=1

DROP FUNCTION IF EXISTS lucyapi.fn_hint_category_create(p_user_id integer, p_parent_id integer, p_title text, p_description text, p_sort_order integer);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_category_create(p_user_id integer, p_title text, p_description text);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_category_delete(p_hint_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_create(p_user_id integer, p_parent_id integer, p_title text, p_description text, p_sort_order integer);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_delete(p_hint_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_get(p_hint_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_update(p_hint_id integer, p_title text, p_description text);
DROP FUNCTION IF EXISTS lucyapi.fn_hint_update(p_hint_id integer, p_title text, p_description text, p_sort_order integer);
DROP FUNCTION IF EXISTS lucyapi.fn_image_delete(p_image_id integer, p_force boolean);
DROP FUNCTION IF EXISTS lucyapi.fn_image_delete_batch(p_image_ids integer[]);
DROP FUNCTION IF EXISTS lucyapi.fn_image_get(p_image_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_image_update_keep(p_image_id integer, p_keep boolean);
DROP FUNCTION IF EXISTS lucyapi.fn_project_delete(p_project_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_project_get_sections(p_project_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_project_update(p_project_id integer, p_title text, p_description text, p_status_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_section_create(p_project_id integer, p_parent_id integer, p_title text, p_description text, p_file_path text);
DROP FUNCTION IF EXISTS lucyapi.fn_section_delete(p_project_id integer, p_section_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_section_get(p_project_id integer, p_section_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_section_get_all_compact(p_project_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_section_update(p_project_id integer, p_section_id integer, p_title text, p_description text, p_file_path text);
DROP FUNCTION IF EXISTS lucyapi.fn_share_create(p_shared_by_user_id integer, p_shared_to_user_id integer, p_object_type_id smallint, p_object_id integer, p_permission_level smallint);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_delete(p_wiki_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_get_sections(p_wiki_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_section_create(p_wiki_id integer, p_parent_id integer, p_title text, p_description text, p_tags text[]);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_section_delete(p_wiki_id integer, p_section_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_section_get(p_wiki_id integer, p_section_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_section_update(p_wiki_id integer, p_section_id integer, p_title text, p_description text, p_tags text[]);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_tags_get(p_wiki_id integer);
DROP FUNCTION IF EXISTS lucyapi.fn_wiki_update(p_wiki_id integer, p_title text, p_description text);
