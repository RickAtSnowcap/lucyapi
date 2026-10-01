-- rebuild.sql
-- Master psql script for the lucyapi database.
-- Recreates all objects in dependency order. Safe for re-runs:
--   - Tables use IF NOT EXISTS
--   - Functions use CREATE OR REPLACE
--   - Indexes use IF NOT EXISTS
--   - Seed data uses ON CONFLICT ... DO UPDATE
--   - Grants are idempotent
--
-- Usage:  psql -d lucyapi -f /opt/lucyapi/database/rebuild.sql

\echo '=== LucyAPI Database Rebuild ==='
\echo ''

-- 1. Extensions
\echo '--- Extensions ---'
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 2. Schema
\echo '--- Schema ---'
CREATE SCHEMA IF NOT EXISTS lucyapi;

-- 3. Tables (parents before children)
\echo '--- Tables ---'
\i tables/users.sql
\i tables/agents.sql
\i tables/project_statuses.sql
\i tables/object_types.sql
\i tables/always_load.sql
\i tables/memories.sql
\i tables/preferences.sql
\i tables/projects.sql
\i tables/project_sections.sql
\i tables/sessions.sql
\i tables/secrets.sql
\i tables/handoffs.sql
\i tables/hints.sql
\i tables/images.sql
\i tables/wikis.sql
\i tables/wiki_sections.sql
\i tables/wiki_section_tags.sql
\i tables/shared_objects.sql
\i tables/nudges.sql
\i tables/oauth_clients.sql
\i tables/oauth_auth_codes.sql
\i tables/oauth_tokens.sql
\i tables/session_projects.sql

-- 4. Functions
\echo '--- Functions ---'
\i functions/fn_access_level.sql
\i functions/fn_agent_get_by_api_key.sql
\i functions/fn_agent_get_by_name.sql
\i functions/fn_memory_get_all.sql
\i functions/fn_memory_get_one.sql
\i functions/fn_memory_create.sql
\i functions/fn_memory_update.sql
\i functions/fn_memory_delete.sql
\i functions/fn_preference_get_all.sql
\i functions/fn_preference_get_top_level.sql
\i functions/fn_preference_get_branch.sql
\i functions/fn_preference_create.sql
\i functions/fn_preference_update.sql
\i functions/fn_preference_delete.sql
\i functions/fn_handoff_list_pending.sql
\i functions/fn_handoff_get.sql
\i functions/fn_handoff_create.sql
\i functions/fn_handoff_pickup.sql
\i functions/fn_handoff_delete.sql
\i functions/fn_session_add_project.sql
\i functions/fn_session_set_description.sql
\i functions/fn_nudge_create.sql
\i functions/fn_nudge_get.sql
\i functions/fn_nudge_get_all.sql
\i functions/fn_nudge_update.sql
\i functions/fn_nudge_delete.sql
\i functions/fn_nudge_get_actionable.sql
\i functions/fn_context_get_full.sql
\i functions/fn_project_statuses_get.sql
\i functions/fn_project_get_all.sql
\i functions/fn_project_get.sql
\i functions/fn_project_get_compact.sql
\i functions/fn_project_get_sections.sql
\i functions/fn_section_get_all_compact.sql
\i functions/fn_project_create.sql
\i functions/fn_project_update.sql
\i functions/fn_project_delete.sql
\i functions/fn_section_get.sql
\i functions/fn_section_create.sql
\i functions/fn_section_update.sql
\i functions/fn_section_delete.sql
\i functions/fn_wiki_get_all.sql
\i functions/fn_wiki_get.sql
\i functions/fn_wiki_get_sections.sql
\i functions/fn_wiki_create.sql
\i functions/fn_wiki_update.sql
\i functions/fn_wiki_delete.sql
\i functions/fn_wiki_section_get.sql
\i functions/fn_wiki_section_create.sql
\i functions/fn_wiki_section_update.sql
\i functions/fn_wiki_section_delete.sql
\i functions/fn_wiki_tags_get.sql
\i functions/fn_wiki_tag_search.sql
\i functions/fn_always_load_get_all.sql
\i functions/fn_always_load_get_item.sql
\i functions/fn_always_load_create.sql
\i functions/fn_always_load_update.sql
\i functions/fn_always_load_delete.sql
\i functions/fn_hint_get_all.sql
\i functions/fn_hint_get_compact.sql
\i functions/fn_hint_get.sql
\i functions/fn_hint_category_create.sql
\i functions/fn_hint_create.sql
\i functions/fn_hint_update.sql
\i functions/fn_hint_delete.sql
\i functions/fn_hint_category_delete.sql
\i functions/fn_image_insert.sql
\i functions/fn_image_get.sql
\i functions/fn_image_list.sql
\i functions/fn_image_update_keep.sql
\i functions/fn_image_delete.sql
\i functions/fn_image_get_unkept.sql
\i functions/fn_image_delete_batch.sql
\i functions/fn_secret_list.sql
\i functions/fn_secret_get.sql
\i functions/fn_secret_set.sql
\i functions/fn_secret_delete.sql
\i functions/fn_share_create.sql
\i functions/fn_share_revoke.sql
\i functions/fn_share_list_by_me.sql
\i functions/fn_share_list_to_me.sql
\i functions/fn_share_check_permission.sql
\i functions/fn_oauth_client_upsert.sql
\i functions/fn_oauth_client_get.sql
\i functions/fn_oauth_user_for_login.sql
\i functions/fn_oauth_agents_for_user.sql
\i functions/fn_oauth_code_create.sql
\i functions/fn_oauth_code_consume.sql
\i functions/fn_oauth_tokens_issue.sql
\i functions/fn_oauth_refresh_rotate.sql
\i functions/fn_oauth_access_resolve.sql
\i functions/fn_oauth_revoke_agent.sql
\i functions/fn_oauth_purge_expired.sql
\i functions/fn_user_get_by_username.sql
\i functions/fn_user_get.sql
\i functions/fn_user_list_others.sql
\i functions/fn_user_set_password_hash.sql
\i functions/fn_admin_agent_list.sql
\i functions/fn_admin_share_list_by_me.sql
\i functions/fn_admin_share_list_to_me.sql
\i functions/fn_admin_dashboard_stats.sql
\i functions/fn_admin_session_list_recent.sql
\i functions/fn_admin_session_list_by_agent.sql
\i functions/fn_share_update_permission.sql

-- 5. Seed data
\echo '--- Seed Data ---'
\i seed/project_statuses.sql
\i seed/object_types.sql

-- 6. Grants
\echo '--- Grants ---'
\i grants.sql

\echo ''
\echo '=== Rebuild complete ==='
