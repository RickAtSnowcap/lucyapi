-- grants.sql
-- All permissions for the leaddev application role.
-- Run after all tables, functions, and seed data are created.

-- Schema
GRANT USAGE ON SCHEMA lucyapi TO leaddev;

-- Tables (SELECT, INSERT, UPDATE, DELETE)
GRANT SELECT, INSERT, UPDATE, DELETE ON
    users,
    agents,
    always_load,
    memories,
    preferences,
    project_statuses,
    object_types,
    projects,
    project_sections,
    sessions,
    secrets,
    handoffs,
    hints,
    images,
    wikis,
    wiki_sections,
    wiki_section_tags,
    shared_objects,
    nudges
TO lucyapi_app;

-- Sequences (USAGE, SELECT)
GRANT USAGE, SELECT ON
    users_user_id_seq,
    agents_agent_id_seq,
    always_load_pkid_seq,
    memories_pkid_seq,
    preferences_pkid_seq,
    project_statuses_status_id_seq,
    projects_project_id_seq,
    project_sections_section_id_seq,
    sessions_session_id_seq,
    secrets_secret_id_seq,
    handoffs_handoff_id_seq,
    hints_hint_id_seq,
    images_image_id_seq,
    wikis_wiki_id_seq,
    wiki_sections_section_id_seq,
    wiki_section_tags_tag_id_seq,
    shared_objects_share_id_seq,
    nudges_nudge_id_seq
TO lucyapi_app;

-- Functions
GRANT EXECUTE ON FUNCTION lucyapi.fn_agent_get_by_api_key(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_agent_get_by_name(TEXT) TO leaddev;
-- OAuth (project #62) — leaddev owns these objects; grants listed for completeness
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_client_upsert(TEXT, TEXT, TEXT, TEXT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_client_get(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_user_for_login(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_agents_for_user(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_code_create(TEXT, TEXT, TEXT, TEXT, TEXT, TEXT, INT, INT, TIMESTAMPTZ) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_code_consume(TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_tokens_issue(UUID, TEXT, TEXT, TEXT, INT, INT, TEXT, TEXT, TIMESTAMPTZ, TIMESTAMPTZ) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_refresh_rotate(TEXT, TEXT, TEXT, TEXT, TIMESTAMPTZ, TIMESTAMPTZ) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_access_resolve(TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_revoke_agent(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_oauth_purge_expired() TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_get_all(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_get_one(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_create(INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_update(INT, INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_memory_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_get_top_level(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_get_branch(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_create(INT, INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_update(INT, INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_preference_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_list_pending(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_get(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_create(INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_pickup(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_handoff_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_session_create(INT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_session_get_last(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_context_get_full(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_statuses_get() TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get_all(INT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get_compact(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_get_sections(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_get_all_compact(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_create(INT, TEXT, TEXT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_update(INT, TEXT, TEXT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_project_delete(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_get(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_create(INT, INT, TEXT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_update(INT, INT, TEXT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_section_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_get_all(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_get(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_get_sections(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_create(INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_update(INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_delete(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_get(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_create(INT, INT, TEXT, TEXT, TEXT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_update(INT, INT, TEXT, TEXT, TEXT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_section_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_tags_get(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_wiki_tag_search(TEXT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_get_all(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_get_item(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_create(INT, INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_update(INT, INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_always_load_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_get_all(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_get_compact(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_get(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_category_create(INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_create(INT, INT, TEXT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_update(INT, TEXT, TEXT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_hint_delete(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_list(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_get(INT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_set(INT, TEXT, BYTEA) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_secret_delete(INT, TEXT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_create(INT, INT, SMALLINT, INT, SMALLINT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_revoke(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_list_by_me(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_list_to_me(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_share_check_permission(INT, SMALLINT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_insert(INT, TEXT, TEXT, TEXT, INT, INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_get(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_list(INT, BOOLEAN, INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_update_keep(INT, BOOLEAN) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_delete(INT, BOOLEAN) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_get_unkept(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_image_delete_batch(INT[]) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_create(INT, TEXT, TEXT, DATE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_get(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_get_all(INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_update(INT, INT, TEXT, TEXT, DATE) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_delete(INT, INT) TO leaddev;
GRANT EXECUTE ON FUNCTION lucyapi.fn_nudge_get_actionable(INT) TO leaddev;
