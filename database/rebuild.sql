-- rebuild.sql
-- Master psql script for the lucyapi database.
-- Recreates all objects in dependency order. Safe for re-runs:
--   - Tables use IF NOT EXISTS
--   - Functions use CREATE OR REPLACE
--   - Indexes use IF NOT EXISTS
--   - Seed data uses ON CONFLICT ... DO UPDATE
--   - Grants are idempotent
--
-- Also builds a fresh database: see README.md in this folder for the full steps (role, database, first user).
--
-- Usage:  psql -d lucyapi -v ON_ERROR_STOP=1 [-v owner_role=<role>] [-v app_role=<role>] -f database/rebuild.sql
--   owner_role: the role that owns every object (default: whoever runs the script). Run as a member of it.
--   app_role:   the login LucyAPI connects as (default lucyapi_app). It gets only the grants in grants.sql.
--   Both roles must already exist.
-- Includes use \ir, so the script runs from any working directory.

\if :{?app_role}
\else
    \set app_role lucyapi_app
\endif
\if :{?owner_role}
    SET ROLE :"owner_role";
\endif

\echo '=== LucyAPI Database Rebuild ==='
\echo ''

-- 1. Extensions: none. LucyAPI uses only built-in PostgreSQL functions.

-- 2. Schema
\echo '--- Schema ---'
CREATE SCHEMA IF NOT EXISTS lucyapi;

-- 3. Tables (parents before children)
\echo '--- Tables ---'
\ir tables/users.sql
\ir tables/agents.sql
\ir tables/project_statuses.sql
\ir tables/object_types.sql
\ir tables/always_load.sql
\ir tables/memories.sql
\ir tables/preferences.sql
\ir tables/projects.sql
\ir tables/project_sections.sql
\ir tables/sessions.sql
\ir tables/handoffs.sql
\ir tables/hints.sql
\ir tables/images.sql
\ir tables/wikis.sql
\ir tables/wiki_sections.sql
\ir tables/wiki_section_tags.sql
\ir tables/shared_objects.sql
\ir tables/nudges.sql
\ir tables/oauth_clients.sql
\ir tables/oauth_auth_codes.sql
\ir tables/oauth_tokens.sql
\ir tables/session_projects.sql

-- 4. Functions
\echo '--- Functions ---'
\ir functions/fn_access_level.sql
\ir functions/fn_agent_get_by_name.sql
\ir functions/fn_memory_get_all.sql
\ir functions/fn_memory_get_one.sql
\ir functions/fn_memory_create.sql
\ir functions/fn_memory_update.sql
\ir functions/fn_memory_delete.sql
\ir functions/fn_preference_get_all.sql
\ir functions/fn_preference_get_top_level.sql
\ir functions/fn_preference_get_branch.sql
\ir functions/fn_preference_create.sql
\ir functions/fn_preference_update.sql
\ir functions/fn_preference_delete.sql
\ir functions/fn_handoff_list_pending.sql
\ir functions/fn_handoff_list_sent.sql
\ir functions/fn_handoff_get.sql
\ir functions/fn_handoff_create.sql
\ir functions/fn_handoff_pickup.sql
\ir functions/fn_handoff_update.sql
\ir functions/fn_handoff_delete.sql
\ir functions/fn_session_add_project.sql
\ir functions/fn_session_set_description.sql
\ir functions/fn_nudge_create.sql
\ir functions/fn_nudge_get.sql
\ir functions/fn_nudge_get_all.sql
\ir functions/fn_nudge_update.sql
\ir functions/fn_nudge_delete.sql
\ir functions/fn_nudge_get_actionable.sql
\ir functions/fn_context_get_full.sql
\ir functions/fn_project_statuses_get.sql
\ir functions/fn_project_get_all.sql
\ir functions/fn_project_get.sql
\ir functions/fn_project_get_compact.sql
\ir functions/fn_project_get_sections.sql
\ir functions/fn_section_get_all_compact.sql
\ir functions/fn_project_create.sql
\ir functions/fn_project_update.sql
\ir functions/fn_project_delete.sql
\ir functions/fn_section_get.sql
\ir functions/fn_section_create.sql
\ir functions/fn_section_update.sql
\ir functions/fn_section_delete.sql
\ir functions/fn_wiki_get_all.sql
\ir functions/fn_wiki_get.sql
\ir functions/fn_wiki_get_sections.sql
\ir functions/fn_wiki_create.sql
\ir functions/fn_wiki_update.sql
\ir functions/fn_wiki_delete.sql
\ir functions/fn_wiki_section_get.sql
\ir functions/fn_wiki_section_create.sql
\ir functions/fn_wiki_section_update.sql
\ir functions/fn_wiki_section_delete.sql
\ir functions/fn_wiki_tags_get.sql
\ir functions/fn_wiki_tag_search.sql
\ir functions/fn_always_load_get_all.sql
\ir functions/fn_always_load_get_item.sql
\ir functions/fn_always_load_create.sql
\ir functions/fn_always_load_update.sql
\ir functions/fn_always_load_delete.sql
\ir functions/fn_hint_get_all.sql
\ir functions/fn_hint_get_compact.sql
\ir functions/fn_hint_get.sql
\ir functions/fn_hint_category_create.sql
\ir functions/fn_hint_create.sql
\ir functions/fn_hint_update.sql
\ir functions/fn_hint_delete.sql
\ir functions/fn_hint_category_delete.sql
\ir functions/fn_image_insert.sql
\ir functions/fn_image_get.sql
\ir functions/fn_image_list.sql
\ir functions/fn_image_update_keep.sql
\ir functions/fn_image_delete.sql
\ir functions/fn_image_get_unkept.sql
\ir functions/fn_image_delete_batch.sql
\ir functions/fn_share_create.sql
\ir functions/fn_share_revoke.sql
\ir functions/fn_share_list_by_me.sql
\ir functions/fn_share_list_to_me.sql
\ir functions/fn_share_check_permission.sql
\ir functions/fn_oauth_client_upsert.sql
\ir functions/fn_oauth_client_get.sql
\ir functions/fn_oauth_user_for_login.sql
\ir functions/fn_oauth_agents_for_user.sql
\ir functions/fn_oauth_code_create.sql
\ir functions/fn_oauth_code_consume.sql
\ir functions/fn_oauth_tokens_issue.sql
\ir functions/fn_oauth_refresh_rotate.sql
\ir functions/fn_oauth_access_resolve.sql
\ir functions/fn_oauth_revoke_agent.sql
\ir functions/fn_oauth_purge_expired.sql
\ir functions/fn_user_get_by_username.sql
\ir functions/fn_user_get.sql
\ir functions/fn_user_list_others.sql
\ir functions/fn_user_set_password_hash.sql
\ir functions/fn_admin_agent_list.sql
\ir functions/fn_admin_share_list_by_me.sql
\ir functions/fn_admin_share_list_to_me.sql
\ir functions/fn_admin_dashboard_stats.sql
\ir functions/fn_admin_session_list_recent.sql
\ir functions/fn_admin_session_list_by_agent.sql
\ir functions/fn_share_update_permission.sql

-- 5. Seed data
\echo '--- Seed Data ---'
\ir seed/project_statuses.sql
\ir seed/object_types.sql

-- 6. Grants
\echo '--- Grants ---'
\ir grants.sql

\echo ''
\echo '=== Rebuild complete ==='
