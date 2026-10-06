-- grants.sql
-- Permissions for the application role `leaddev` — the role LucyAPI connects as.
-- Run LAST, after all tables, functions and seed data exist (rebuild.sql does this).
--
-- Standing up your own instance: create the role before running rebuild.sql, e.g.
--   CREATE ROLE leaddev LOGIN PASSWORD '<choose one>';
-- and put that login in LucyAPI's (sealed) Suitcase:DbConnection.
--
-- Generated from the live catalog 2026-09-29 (after migrations 007/008); image grants updated for 010, session grants for 011/012, key lookup dropped by 014. Function grants use the
-- EXACT current signatures — when a migration changes a signature, update the matching line here.

-- Schemas
GRANT USAGE ON SCHEMA public TO leaddev;
GRANT USAGE ON SCHEMA lucyapi TO leaddev;

-- Tables
GRANT SELECT, INSERT, UPDATE, DELETE ON
    agents,
    always_load,
    handoffs,
    hints,
    images,
    memories,
    nudges,
    oauth_auth_codes,
    oauth_clients,
    oauth_tokens,
    object_types,
    preferences,
    project_sections,
    project_statuses,
    projects,
    secrets,
    session_projects,
    sessions,
    shared_objects,
    users,
    wiki_section_tags,
    wiki_sections,
    wikis
TO leaddev;

-- Sequences
GRANT USAGE, SELECT ON
    agents_agent_id_seq,
    always_load_pkid_seq,
    handoffs_handoff_id_seq,
    hints_hint_id_seq,
    images_image_id_seq,
    memories_pkid_seq,
    nudges_nudge_id_seq,
    oauth_tokens_token_id_seq,
    preferences_pkid_seq,
    project_sections_section_id_seq,
    project_statuses_status_id_seq,
    projects_project_id_seq,
    secrets_secret_id_seq,
    sessions_session_id_seq,
    shared_objects_share_id_seq,
    users_user_id_seq,
    wiki_section_tags_tag_id_seq,
    wiki_sections_section_id_seq,
    wikis_wiki_id_seq
TO leaddev;

-- Functions
GRANT EXECUTE ON FUNCTION lucyapi.fn_access_level(INTEGER, SMALLINT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_agent_list(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_dashboard_stats(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_session_list_by_agent(INTEGER, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_session_list_recent(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_share_list_by_me(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_admin_share_list_to_me(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_agent_get_by_name(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_create(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_get_all(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_get_item(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_update(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_context_get_full(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_create(INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_list_pending(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_pickup(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_category_create(INTEGER, INTEGER, TEXT, TEXT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_category_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_create(INTEGER, INTEGER, TEXT, TEXT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_get_all(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_get_compact(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_update(INTEGER, INTEGER, TEXT, TEXT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_delete(INTEGER, INTEGER, BOOLEAN) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_delete_batch(INTEGER, INTEGER[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_get_unkept(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_insert(INTEGER, INTEGER, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, BOOLEAN, INTEGER, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_list(INTEGER, BOOLEAN, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_update_keep(INTEGER, INTEGER, BOOLEAN) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_create(INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_get_all(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_get_one(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_update(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_create(INTEGER, TEXT, TEXT, DATE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_get_actionable(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_get_all(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_update(INTEGER, INTEGER, TEXT, TEXT, DATE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_access_resolve(TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_agents_for_user(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_client_get(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_client_upsert(TEXT, TEXT, TEXT, TEXT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_code_consume(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_code_create(TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, INTEGER, INTEGER, TIMESTAMP WITH TIME ZONE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_purge_expired() TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_refresh_rotate(TEXT, TEXT, TEXT, TEXT, TIMESTAMP WITH TIME ZONE, TIMESTAMP WITH TIME ZONE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_revoke_agent(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_tokens_issue(UUID, TEXT, TEXT, TEXT, INTEGER, INTEGER, TEXT, TEXT, TIMESTAMP WITH TIME ZONE, TIMESTAMP WITH TIME ZONE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_user_for_login(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_create(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_get_all(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_get_branch(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_get_top_level(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_update(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_create(INTEGER, TEXT, TEXT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get_all(INTEGER, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get_compact(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get_sections(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_statuses_get() TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_update(INTEGER, INTEGER, TEXT, TEXT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_delete(INTEGER, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_get(INTEGER, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_list(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_set(INTEGER, TEXT, BYTEA) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_create(INTEGER, INTEGER, INTEGER, TEXT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_delete(INTEGER, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_get(INTEGER, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_get_all_compact(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_update(INTEGER, INTEGER, INTEGER, TEXT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_session_add_project(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_session_set_description(INTEGER, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_check_permission(INTEGER, SMALLINT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_create(INTEGER, INTEGER, SMALLINT, INTEGER, SMALLINT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_list_by_me(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_list_to_me(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_revoke(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_update_permission(INTEGER, INTEGER, SMALLINT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_user_get(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_user_get_by_username(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_user_list_others(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_user_set_password_hash(INTEGER, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_create(INTEGER, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_delete(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_get_all(INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_get_sections(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_create(INTEGER, INTEGER, INTEGER, TEXT, TEXT, TEXT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_delete(INTEGER, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_get(INTEGER, INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_update(INTEGER, INTEGER, INTEGER, TEXT, TEXT, TEXT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_tag_search(TEXT, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_tags_get(INTEGER, INTEGER) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_update(INTEGER, INTEGER, TEXT, TEXT) TO leaddev;
