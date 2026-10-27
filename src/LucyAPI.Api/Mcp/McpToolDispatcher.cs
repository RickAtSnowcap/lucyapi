using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LucyAPI.Api.Auth;
using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Mcp;

public sealed class McpToolDispatcher(
    IAgentService agentService,
    IAlwaysLoadService alwaysLoadService,
    IMemoryService memoryService,
    IPreferenceService preferenceService,
    IProjectService projectService,
    ISectionService sectionService,
    IWikiService wikiService,
    IWikiSectionService wikiSectionService,
    IWikiTagService wikiTagService,
    IHandoffService handoffService,
    ISessionService sessionService,
    IContextService contextService,
    IHintService hintService,
    INudgeService nudgeService,
    IShareService shareService,
    IGoogleDocsService googleDocsService,
    IImageService imageService,
    ISaveNotesService saveNotesService,
    DocumentLinkSigner documentLinks)
{
    private static readonly JsonSerializerOptions s_snakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = AppJsonSerializerContext.Default
    };

    private string? _toolListCache;

    // ---------------------------------------------------------------
    //  Tool list (cached JSON for tools/list response)
    // ---------------------------------------------------------------
    public string GetToolListJson() => _toolListCache ??= BuildToolListJson();

    // ---------------------------------------------------------------
    //  Dispatch
    // ---------------------------------------------------------------
    // The caller is the agent bound to the OAuth bearer token (/mcp/connector); there is no other way in.
    public async Task<string> DispatchAsync(string toolName, JsonElement args, Agent caller, CancellationToken ct)
    {
        return toolName switch
        {
            // --- System ---
            "get_time" => HandleGetTime(),

            // --- Context ---
            "get_context" => await HandleGetContext(caller, args, ct),
            "get_always_load" => await HandleGetAlwaysLoad(caller, args, ct),
            "get_always_load_item" => await HandleGetAlwaysLoadItem(caller, args, ct),
            "create_always_load" => await HandleCreateAlwaysLoad(caller, args, ct),
            "update_always_load" => await HandleUpdateAlwaysLoad(caller, args, ct),
            "delete_always_load" => await HandleDeleteAlwaysLoad(caller, args, ct),

            // --- Memories ---
            "get_memories" => await HandleGetMemories(caller, args, ct),
            "get_memory" => await HandleGetMemory(caller, args, ct),
            "create_memory" => await HandleCreateMemory(caller, args, ct),
            "update_memory" => await HandleUpdateMemory(caller, args, ct),
            "delete_memory" => await HandleDeleteMemory(caller, args, ct),

            // --- Preferences ---
            "get_preferences" => await HandleGetPreferences(caller, args, ct),
            "get_preference" => await HandleGetPreference(caller, args, ct),
            "create_preference" => await HandleCreatePreference(caller, args, ct),
            "update_preference" => await HandleUpdatePreference(caller, args, ct),
            "delete_preference" => await HandleDeletePreference(caller, args, ct),

            // --- Projects ---
            "get_project_statuses" => await HandleGetProjectStatuses(ct),
            "get_projects" => await HandleGetProjects(caller, args, ct),
            "get_project" => await HandleGetProject(caller, args, ct),
            "get_project_compact" => await HandleGetProjectCompact(caller, args, ct),
            "get_section" => await HandleGetSection(caller, args, ct),
            "create_project" => await HandleCreateProject(caller, args, ct),
            "create_section" => await HandleCreateSection(caller, args, ct),
            "update_project" => await HandleUpdateProject(caller, args, ct),
            "update_section" => await HandleUpdateSection(caller, args, ct),
            "delete_project" => await HandleDeleteProject(caller, args, ct),
            "delete_section" => await HandleDeleteSection(caller, args, ct),

            // --- Hints ---
            "get_hints" => await HandleGetHints(caller, ct),
            "get_hints_compact" => await HandleGetHintsCompact(caller, ct),
            "get_hint" => await HandleGetHint(caller, args, ct),
            "create_hint_category" => await HandleCreateHintCategory(caller, args, ct),
            "create_hint" => await HandleCreateHint(caller, args, ct),
            "update_hint" => await HandleUpdateHint(caller, args, ct),
            "delete_hint" => await HandleDeleteHint(caller, args, ct),
            "delete_hint_category" => await HandleDeleteHintCategory(caller, args, ct),

            // --- Wikis ---
            "get_wikis" => await HandleGetWikis(caller, ct),
            "get_wiki" => await HandleGetWiki(caller, args, ct),
            "create_wiki" => await HandleCreateWiki(caller, args, ct),
            "update_wiki" => await HandleUpdateWiki(caller, args, ct),
            "delete_wiki" => await HandleDeleteWiki(caller, args, ct),
            "create_wiki_section" => await HandleCreateWikiSection(caller, args, ct),
            "get_wiki_section" => await HandleGetWikiSection(caller, args, ct),
            "update_wiki_section" => await HandleUpdateWikiSection(caller, args, ct),
            "delete_wiki_section" => await HandleDeleteWikiSection(caller, args, ct),
            "get_wiki_tags" => await HandleGetWikiTags(caller, args, ct),
            "search_wiki_tag" => await HandleSearchWikiTag(caller, args, ct),

            // --- Sharing ---
            "share_object" => await HandleShareObject(caller, args, ct),
            "revoke_share" => await HandleRevokeShare(caller, args, ct),
            "get_shared_by_me" => await HandleGetSharedByMe(caller, ct),
            "get_shared_to_me" => await HandleGetSharedToMe(caller, ct),
            "check_access" => await HandleCheckAccess(caller, args, ct),

            // --- Sessions (opened by get_context) ---
            "set_session_description" => await HandleSetSessionDescription(caller, args, ct),

            // --- Save Notes ---
            "save_notes" => HandleSaveNotes(args),

            // --- Nudges ---
            "get_nudges" => await HandleGetNudges(caller, ct),
            "get_nudge" => await HandleGetNudge(caller, args, ct),
            "create_nudge" => await HandleCreateNudge(caller, args, ct),
            "update_nudge" => await HandleUpdateNudge(caller, args, ct),
            "delete_nudge" => await HandleDeleteNudge(caller, args, ct),

            // --- Handoffs ---
            "list_handoffs" => await HandleListHandoffs(caller, args, ct),
            "get_handoff" => await HandleGetHandoff(caller, args, ct),
            "create_handoff" => await HandleCreateHandoff(caller, args, ct),
            "pickup_handoff" => await HandlePickupHandoff(caller, args, ct),
            "delete_handoff" => await HandleDeleteHandoff(caller, args, ct),
            "update_handoff" => await HandleUpdateHandoff(caller, args, ct),
            "list_sent_handoffs" => await HandleListSentHandoffs(caller, args, ct),

            // --- Images ---
            "generate_image" => await HandleGenerateImage(caller, args, ct),
            "edit_image" => await HandleEditImage(caller, args, ct),
            "upload_image" => await HandleUploadImage(caller, args, ct),
            "analyze_image" => await HandleAnalyzeImage(caller, args, ct),
            "list_images" => await HandleListImages(caller, args, ct),
            "get_image" => await HandleGetImage(caller, args, ct),
            "keep_image" => await HandleKeepImage(caller, args, ct),
            "delete_image" => await HandleDeleteImage(caller, args, ct),
            "cleanup_images" => await HandleCleanupImages(caller, ct),

            // --- Google Docs ---
            "create_google_doc" => await HandleCreateGoogleDoc(caller, args, ct),
            "read_google_doc" => await HandleReadGoogleDoc(caller, args, ct),
            "get_doc_images" => await HandleGetDocImages(caller, args, ct),
            "append_doc_image" => await HandleAppendDocImage(caller, args, ct),
            "update_google_doc" => await HandleUpdateGoogleDoc(caller, args, ct),
            "append_google_doc" => await HandleAppendGoogleDoc(caller, args, ct),

            // --- Google Drive ---
            "list_google_files" => await HandleListGoogleFiles(caller, args, ct),
            "create_google_folder" => await HandleCreateGoogleFolder(caller, args, ct),
            "move_google_file" => await HandleMoveGoogleFile(caller, args, ct),
            "delete_google_file" => await HandleDeleteGoogleFile(caller, args, ct),
            "get_google_file_meta" => await HandleGetGoogleFileMeta(caller, args, ct),

            _ => Error("Unknown tool: " + toolName)
        };
    }

    // ===============================================================
    //  HANDLERS: System
    // ===============================================================

    private static string HandleGetTime()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var mountainZone = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var mountainNow = TimeZoneInfo.ConvertTime(utcNow, mountainZone);
        var tz = mountainZone.IsDaylightSavingTime(mountainNow) ? "MDT" : "MST";
        var dow = mountainNow.DayOfWeek.ToString();
        return "{\"utc_time\":\"" + utcNow.ToString("yyyy-MM-dd HH:mm:ss") +
               "\",\"mountain_time\":\"" + mountainNow.ToString("yyyy-MM-dd HH:mm:ss") +
               "\",\"timezone\":\"" + tz +
               "\",\"day_of_week\":\"" + dow + "\"}";
    }

    // ===============================================================
    //  HANDLERS: Context & Always-Load
    // ===============================================================

    private async Task<string> HandleGetContext(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var doc = await contextService.GetFullAsync(agentId, caller.UserId, ct);
        if (doc is null) return "{}";
        return doc.RootElement.GetRawText();
    }

    private async Task<string> HandleGetAlwaysLoad(Agent caller, JsonElement args, CancellationToken ct)
    {
        var (agentId, agentName) = ResolveAgent(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var items = await alwaysLoadService.GetAllAsync(agentId, ct);
        var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId);
        var treeJson = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeAlwaysLoadItem);
        return "{\"agent\":\"" + Esc(agentName) + "\",\"always_load\":" + treeJson + "}";
    }

    private async Task<string> HandleGetAlwaysLoadItem(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var pkid = GetInt(args, "pkid");
        var items = await alwaysLoadService.GetItemAsync(agentId, pkid, ct);
        if (items.Count == 0) return Error("Not found");
        var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items[0].ParentId);
        var treeJson = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeAlwaysLoadItem);
        return treeJson.Length > 2 ? treeJson[1..^1] : "null"; // unwrap array to single node
    }

    private async Task<string> HandleCreateAlwaysLoad(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var req = new CreateAlwaysLoadRequest
        {
            ParentId = GetInt(args, "parent_id", 0),
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? ""
        };
        var result = await alwaysLoadService.CreateAsync(agentId, req, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.AlwaysLoadCreated);
    }

    private async Task<string> HandleUpdateAlwaysLoad(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var req = new UpdateAlwaysLoadRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description")
        };
        var result = await alwaysLoadService.UpdateAsync(agentId, GetInt(args, "pkid"), req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.MutationResult);
    }

    private async Task<string> HandleDeleteAlwaysLoad(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var count = await alwaysLoadService.DeleteAsync(agentId, GetInt(args, "pkid"), ct);
        return "{\"deleted_count\":" + count + "}";
    }

    // ===============================================================
    //  HANDLERS: Memories
    // ===============================================================

    private async Task<string> HandleGetMemories(Agent caller, JsonElement args, CancellationToken ct)
    {
        var (agentId, agentName) = ResolveAgent(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var items = await memoryService.GetAllAsync(agentId, ct);
        var json = Serialize(items, AppJsonSerializerContext.Default.ListMemory);
        return "{\"agent\":\"" + Esc(agentName) + "\",\"memories\":" + json + "}";
    }

    private async Task<string> HandleGetMemory(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var item = await memoryService.GetOneAsync(agentId, GetInt(args, "pkid"), ct);
        return item is null ? Error("Not found") : Serialize(item, AppJsonSerializerContext.Default.Memory);
    }

    private async Task<string> HandleCreateMemory(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var req = new CreateMemoryRequest
        {
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? ""
        };
        var result = await memoryService.CreateAsync(agentId, req, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.MemoryCreated);
    }

    private async Task<string> HandleUpdateMemory(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var req = new UpdateMemoryRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description")
        };
        var result = await memoryService.UpdateAsync(agentId, GetInt(args, "pkid"), req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.MutationResult);
    }

    private async Task<string> HandleDeleteMemory(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var count = await memoryService.DeleteAsync(agentId, GetInt(args, "pkid"), ct);
        return "{\"deleted_count\":" + count + "}";
    }

    // ===============================================================
    //  HANDLERS: Preferences
    // ===============================================================

    private async Task<string> HandleGetPreferences(Agent caller, JsonElement args, CancellationToken ct)
    {
        var (agentId, agentName) = ResolveAgent(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var items = await preferenceService.GetTopLevelAsync(agentId, ct);
        var json = Serialize(items, AppJsonSerializerContext.Default.ListPreferenceTopLevel);
        return "{\"agent\":\"" + Esc(agentName) + "\",\"preferences\":" + json + "}";
    }

    private async Task<string> HandleGetPreference(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var items = await preferenceService.GetBranchAsync(agentId, GetInt(args, "pkid"), ct);
        var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items.Count > 0 ? items[0].ParentId : 0);
        var treeJson = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodePreference);
        return treeJson.Length > 2 ? treeJson[1..^1] : "null";
    }

    private async Task<string> HandleCreatePreference(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var req = new CreatePreferenceRequest
        {
            ParentId = GetInt(args, "parent_id", 0),
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? ""
        };
        var result = await preferenceService.CreateAsync(agentId, req, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.MutationResult);
    }

    private async Task<string> HandleUpdatePreference(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var req = new UpdatePreferenceRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description")
        };
        var result = await preferenceService.UpdateAsync(agentId, GetInt(args, "pkid"), req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.MutationResult);
    }

    private async Task<string> HandleDeletePreference(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var count = await preferenceService.DeleteAsync(agentId, GetInt(args, "pkid"), ct);
        return "{\"deleted_count\":" + count + "}";
    }

    // ===============================================================
    //  HANDLERS: Projects
    // ===============================================================

    private async Task<string> HandleGetProjectStatuses(CancellationToken ct)
    {
        var items = await projectService.GetStatusesAsync(ct);
        var json = Serialize(items, AppJsonSerializerContext.Default.ListProjectStatus);
        return "{\"statuses\":" + json + "}";
    }

    private async Task<string> HandleGetProjects(Agent caller, JsonElement args, CancellationToken ct)
    {
        var status = GetString(args, "status");
        var items = await projectService.GetAllAsync(caller.UserId, status, ct);
        var json = Serialize(items, AppJsonSerializerContext.Default.ListProject);
        return "{\"projects\":" + json + "}";
    }

    private async Task<string> HandleGetProject(Agent caller, JsonElement args, CancellationToken ct)
    {
        var projectId = GetInt(args, "project_id");
        var project = await projectService.GetAsync(projectId, caller.UserId, ct);
        if (project is null) return Error("Project not found");
        await sessionService.TryAddProjectAsync(caller.AgentId, projectId, ct);   // the session's focus, recorded invisibly

        project.DocumentUrl = documentLinks.CreateProjectUrl(projectId, caller.UserId);

        var sections = await sectionService.GetSectionsAsync(caller.UserId, projectId, ct);
        var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
        var pJson = Serialize(project, AppJsonSerializerContext.Default.Project);
        var sJson = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeProjectSection);
        return "{\"project\":" + pJson + ",\"sections\":" + sJson + "}";
    }

    private async Task<string> HandleGetProjectCompact(Agent caller, JsonElement args, CancellationToken ct)
    {
        var projectId = GetInt(args, "project_id");
        var project = await projectService.GetCompactAsync(projectId, caller.UserId, ct);
        if (project is null) return Error("Project not found");
        await sessionService.TryAddProjectAsync(caller.AgentId, projectId, ct);

        var sections = await sectionService.GetSectionsCompactAsync(caller.UserId, projectId, ct);
        var pJson = Serialize(project, AppJsonSerializerContext.Default.ProjectCompact);
        var sJson = Serialize(sections, AppJsonSerializerContext.Default.ListProjectSectionCompact);
        return "{\"project\":" + pJson + ",\"sections\":" + sJson + "}";
    }

    private async Task<string> HandleGetSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var projectId = GetInt(args, "project_id");
        var sectionId = GetInt(args, "section_id");
        var items = await sectionService.GetAsync(caller.UserId, projectId, sectionId, ct);
        if (items.Count == 0) return Error("Section not found");
        var tree = TreeBuilder.Build(items, s => s.SectionId, s => s.ParentId, items[0].ParentId);
        var json = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeProjectSection);
        return json.Length > 2 ? json[1..^1] : "null";
    }

    private async Task<string> HandleCreateProject(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new CreateProjectRequest
        {
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? "",
            StatusId = GetInt(args, "status_id", 1)
        };
        var result = await projectService.CreateAsync(caller.UserId, req, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.ProjectCreated);
    }

    private async Task<string> HandleCreateSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var projectId = GetInt(args, "project_id");
        var req = new CreateSectionRequest
        {
            ParentId = GetInt(args, "parent_id", 0),
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? "",
            FilePath = GetString(args, "file_path")
        };
        var result = await sectionService.CreateAsync(caller.UserId, projectId, req, ct);
        return result is null ? Error("Project or parent section not found") : Serialize(result, AppJsonSerializerContext.Default.SectionCreated);
    }

    private async Task<string> HandleUpdateProject(Agent caller, JsonElement args, CancellationToken ct)
    {
        var projectId = GetInt(args, "project_id");
        var req = new UpdateProjectRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description"),
            StatusId = GetNullableInt(args, "status_id")
        };
        var result = await projectService.UpdateAsync(caller.UserId, projectId, req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.ProjectCreated);
    }

    private async Task<string> HandleUpdateSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var projectId = GetInt(args, "project_id");
        var sectionId = GetInt(args, "section_id");
        var req = new UpdateSectionRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description"),
            FilePath = GetString(args, "file_path")
        };
        var result = await sectionService.UpdateAsync(caller.UserId, projectId, sectionId, req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.SectionCreated);
    }

    private async Task<string> HandleDeleteProject(Agent caller, JsonElement args, CancellationToken ct)
    {
        var count = await projectService.DeleteAsync(caller.UserId, GetInt(args, "project_id"), ct);
        return count is int deleted ? "{\"deleted_count\":" + deleted + "}" : Error("Project not found");
    }

    private async Task<string> HandleDeleteSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var count = await sectionService.DeleteAsync(caller.UserId, GetInt(args, "project_id"), GetInt(args, "section_id"), ct);
        return count > 0 ? "{\"sections_deleted\":" + count + "}" : Error("Section not found");
    }

    // ===============================================================
    //  HANDLERS: Hints
    // ===============================================================

    private async Task<string> HandleGetHints(Agent caller, CancellationToken ct)
    {
        var items = await hintService.GetAllAsync(caller.UserId, ct);
        var tree = TreeBuilder.Build(items, h => h.Pkid, h => h.ParentId);
        return "{\"hints\":" + Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeHint) + "}";
    }

    private async Task<string> HandleGetHintsCompact(Agent caller, CancellationToken ct)
    {
        var items = await hintService.GetAllCompactAsync(caller.UserId, ct);
        var tree = TreeBuilder.Build(items, h => h.Pkid, h => h.ParentId);
        return "{\"hints\":" + Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeHintCompact) + "}";
    }

    private async Task<string> HandleGetHint(Agent caller, JsonElement args, CancellationToken ct)
    {
        var hintId = GetInt(args, "hint_id");
        var items = await hintService.GetAsync(caller.UserId, hintId, ct);
        if (items.Count == 0) return Error("Hint not found");
        var tree = TreeBuilder.Build(items, h => h.Pkid, h => h.ParentId, items[0].ParentId);
        var json = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeHint);
        return json.Length > 2 ? json[1..^1] : "null";
    }

    private async Task<string> HandleCreateHintCategory(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new CreateHintCategoryRequest
        {
            ParentId = GetInt(args, "parent_id", 0),
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? ""
        };
        var result = await hintService.CreateCategoryAsync(caller.UserId, req, ct);
        return result is null ? Error("Parent category not found") : Serialize(result, AppJsonSerializerContext.Default.HintCreated);
    }

    private async Task<string> HandleDeleteHintCategory(Agent caller, JsonElement args, CancellationToken ct)
    {
        var pkid = GetInt(args, "hint_id");
        var count = await hintService.DeleteCategoryAsync(caller.UserId, pkid, ct);
        return count > 0 ? "{\"deleted_count\":" + count + "}" : Error("Hint category not found");
    }

    private async Task<string> HandleCreateHint(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new CreateHintRequest
        {
            ParentId = GetInt(args, "parent_id", 0),
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? ""
        };
        var result = await hintService.CreateAsync(caller.UserId, req, ct);
        return result is null ? Error("Parent hint not found") : Serialize(result, AppJsonSerializerContext.Default.HintCreated);
    }

    private async Task<string> HandleUpdateHint(Agent caller, JsonElement args, CancellationToken ct)
    {
        var hintId = GetInt(args, "hint_id");
        var req = new UpdateHintRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description")
        };
        var result = await hintService.UpdateAsync(caller.UserId, hintId, req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.MutationResult);
    }

    private async Task<string> HandleDeleteHint(Agent caller, JsonElement args, CancellationToken ct)
    {
        var count = await hintService.DeleteAsync(caller.UserId, GetInt(args, "hint_id"), ct);
        return count > 0 ? "{\"deleted_count\":" + count + "}" : Error("Hint not found");
    }

    // ===============================================================
    //  HANDLERS: Wikis
    // ===============================================================

    private async Task<string> HandleGetWikis(Agent caller, CancellationToken ct)
    {
        var items = await wikiService.GetAllAsync(caller.UserId, ct);
        return "{\"wikis\":" + Serialize(items, AppJsonSerializerContext.Default.ListWiki) + "}";
    }

    private async Task<string> HandleGetWiki(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
        if (wiki is null) return Error("Wiki not found");

        wiki.DocumentUrl = documentLinks.CreateWikiUrl(wikiId, caller.UserId);

        var sections = await wikiSectionService.GetSectionsAsync(caller.UserId, wikiId, ct);
        var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
        return "{\"wiki\":" + Serialize(wiki, AppJsonSerializerContext.Default.Wiki) +
               ",\"sections\":" + Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeWikiSection) + "}";
    }

    private async Task<string> HandleCreateWiki(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new CreateWikiRequest
        {
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? ""
        };
        var result = await wikiService.CreateAsync(caller.UserId, req, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.WikiCreated);
    }

    private async Task<string> HandleUpdateWiki(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var req = new UpdateWikiRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description")
        };
        var result = await wikiService.UpdateAsync(caller.UserId, wikiId, req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.WikiCreated);
    }

    private async Task<string> HandleDeleteWiki(Agent caller, JsonElement args, CancellationToken ct)
    {
        var count = await wikiService.DeleteAsync(caller.UserId, GetInt(args, "wiki_id"), ct);
        return count is int deleted ? "{\"deleted_count\":" + deleted + "}" : Error("Wiki not found");
    }

    private async Task<string> HandleCreateWikiSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var req = new CreateWikiSectionRequest
        {
            ParentId = GetInt(args, "parent_id", 0),
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? "",
            Tags = GetStringArray(args, "tags")
        };
        var result = await wikiSectionService.CreateAsync(caller.UserId, wikiId, req, ct);
        return result is null ? Error("Wiki or parent section not found") : Serialize(result, AppJsonSerializerContext.Default.WikiSectionCreated);
    }

    private async Task<string> HandleGetWikiSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var sectionId = GetInt(args, "section_id");
        var items = await wikiSectionService.GetAsync(caller.UserId, wikiId, sectionId, ct);
        if (items.Count == 0) return Error("Section not found");
        var tree = TreeBuilder.Build(items, s => s.SectionId, s => s.ParentId, items[0].ParentId);
        var json = Serialize(tree, AppJsonSerializerContext.Default.ListTreeNodeWikiSection);
        return json.Length > 2 ? json[1..^1] : "null";
    }

    private async Task<string> HandleUpdateWikiSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var sectionId = GetInt(args, "section_id");
        var req = new UpdateWikiSectionRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description"),
            Tags = GetStringArray(args, "tags")
        };
        var result = await wikiSectionService.UpdateAsync(caller.UserId, wikiId, sectionId, req, ct);
        return result is null ? Error("Not found") : Serialize(result, AppJsonSerializerContext.Default.WikiSectionCreated);
    }

    private async Task<string> HandleDeleteWikiSection(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var sectionId = GetInt(args, "section_id");
        var count = await wikiSectionService.DeleteAsync(caller.UserId, wikiId, sectionId, ct);
        return count > 0 ? "{\"deleted_count\":" + count + "}" : Error("Section not found");
    }

    private async Task<string> HandleGetWikiTags(Agent caller, JsonElement args, CancellationToken ct)
    {
        var wikiId = GetInt(args, "wiki_id");
        var tags = await wikiTagService.GetTagsAsync(caller.UserId, wikiId, ct);
        var json = Serialize(tags, AppJsonSerializerContext.Default.ListString);
        return "{\"wiki_id\":" + wikiId + ",\"tags\":" + json + "}";
    }

    private async Task<string> HandleSearchWikiTag(Agent caller, JsonElement args, CancellationToken ct)
    {
        var tag = GetString(args, "tag") ?? "";
        var results = await wikiTagService.SearchAsync(tag, caller.UserId, ct);
        var json = Serialize(results, AppJsonSerializerContext.Default.ListWikiTagSearchResult);
        return "{\"tag\":\"" + Esc(tag) + "\",\"results\":" + json + "}";
    }

    // ===============================================================
    //  HANDLERS: Sharing
    // ===============================================================

    private async Task<string> HandleShareObject(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new ShareObjectRequest
        {
            SharedToUserId = GetInt(args, "shared_to_user_id"),
            ObjectTypeId = GetInt(args, "object_type_id"),
            ObjectId = GetInt(args, "object_id"),
            PermissionLevel = GetInt(args, "permission_level", 1)
        };
        var result = await shareService.ShareAsync(caller.UserId, req, ct);
        return result is null ? Error("Object not found or you don't own it") : Serialize(result, AppJsonSerializerContext.Default.ShareCreated);
    }

    private async Task<string> HandleRevokeShare(Agent caller, JsonElement args, CancellationToken ct)
    {
        var shareId = GetInt(args, "share_id");
        var ok = await shareService.RevokeAsync(caller.UserId, shareId, ct);
        return ok ? "{\"revoked\":" + shareId + "}" : Error("Share not found or not owned by you");
    }

    private async Task<string> HandleGetSharedByMe(Agent caller, CancellationToken ct)
    {
        var items = await shareService.GetSharedByMeAsync(caller.UserId, ct);
        return "{\"shared\":" + Serialize(items, AppJsonSerializerContext.Default.ListShareRecord) + "}";
    }

    private async Task<string> HandleGetSharedToMe(Agent caller, CancellationToken ct)
    {
        var items = await shareService.GetSharedToMeAsync(caller.UserId, ct);
        return "{\"shared\":" + Serialize(items, AppJsonSerializerContext.Default.ListShareRecord) + "}";
    }

    private async Task<string> HandleCheckAccess(Agent caller, JsonElement args, CancellationToken ct)
    {
        var objectTypeId = GetInt(args, "object_type_id");
        var objectId = GetInt(args, "object_id");
        var result = await shareService.CheckPermissionAsync(caller.UserId, objectTypeId, objectId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.SharePermissionCheck);
    }

    // ===============================================================
    //  HANDLERS: Sessions
    // ===============================================================

    private async Task<string> HandleSetSessionDescription(Agent caller, JsonElement args, CancellationToken ct)
    {
        var description = GetString(args, "description");
        if (string.IsNullOrWhiteSpace(description)) return Error("description is required");
        var result = await sessionService.SetDescriptionAsync(caller.AgentId, description, ct);
        return result is null
            ? Error("No session yet: call get_context first (it opens your session)")
            : Serialize(result, AppJsonSerializerContext.Default.SessionDescriptionResponse);
    }

    // ===============================================================
    //  HANDLERS: Save Notes
    // ===============================================================

    private string HandleSaveNotes(JsonElement args)
    {
        var subject = GetString(args, "subject") ?? "";
        var content = GetString(args, "content") ?? "";
        var result = saveNotesService.SaveAndEmail(subject, content);
        return Serialize(result, AppJsonSerializerContext.Default.SaveNotesResponse);
    }

    // ===============================================================
    //  HANDLERS: Nudges
    // ===============================================================

    private async Task<string> HandleGetNudges(Agent caller, CancellationToken ct)
    {
        var nudges = await nudgeService.GetAllAsync(caller.UserId, ct);
        return "{\"nudges\":" + Serialize(nudges, AppJsonSerializerContext.Default.ListNudge) + "}";
    }

    private async Task<string> HandleGetNudge(Agent caller, JsonElement args, CancellationToken ct)
    {
        var nudge = await nudgeService.GetOneAsync(caller.UserId, GetInt(args, "nudge_id"), ct);
        return nudge is null ? Error("Nudge not found") : Serialize(nudge, AppJsonSerializerContext.Default.Nudge);
    }

    private async Task<string> HandleCreateNudge(Agent caller, JsonElement args, CancellationToken ct)
    {
        var dueDateStr = GetString(args, "due_date");
        DateOnly? dueDate = !string.IsNullOrEmpty(dueDateStr) && DateOnly.TryParse(dueDateStr, out var d) ? d : null;
        var req = new CreateNudgeRequest
        {
            Title = GetString(args, "title") ?? "",
            Description = GetString(args, "description") ?? "",
            DueDate = dueDate
        };
        var result = await nudgeService.CreateAsync(caller.UserId, req, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.NudgeCreated);
    }

    private async Task<string> HandleUpdateNudge(Agent caller, JsonElement args, CancellationToken ct)
    {
        var dueDateStr = GetString(args, "due_date");
        DateOnly? dueDate = !string.IsNullOrEmpty(dueDateStr) && DateOnly.TryParse(dueDateStr, out var d) ? d : null;
        var req = new UpdateNudgeRequest
        {
            Title = GetString(args, "title"),
            Description = GetString(args, "description"),
            DueDate = dueDate,
            ClearDueDate = GetBool(args, "clear_due_date")
        };
        var result = await nudgeService.UpdateAsync(caller.UserId, GetInt(args, "nudge_id"), req, ct);
        return result is null ? Error("Nudge not found") : Serialize(result, AppJsonSerializerContext.Default.MutationResult);
    }

    private async Task<string> HandleDeleteNudge(Agent caller, JsonElement args, CancellationToken ct)
    {
        var count = await nudgeService.DeleteAsync(caller.UserId, GetInt(args, "nudge_id"), ct);
        return count == 0 ? Error("Nudge not found") : "{\"deleted_count\":" + count + "}";
    }

    // ===============================================================
    //  HANDLERS: Handoffs
    // ===============================================================

    private async Task<string> HandleListHandoffs(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var items = await handoffService.ListPendingAsync(agentId, ct);
        return "{\"handoffs\":" + Serialize(items, AppJsonSerializerContext.Default.ListHandoff) + "}";
    }

    private async Task<string> HandleGetHandoff(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var handoffId = GetInt(args, "handoff_id");
        var item = await handoffService.GetAsync(agentId, handoffId, ct);
        return item is null ? Error(HandoffNotFound(handoffId)) : Serialize(item, AppJsonSerializerContext.Default.Handoff);
    }

    private async Task<string> HandleCreateHandoff(Agent caller, JsonElement args, CancellationToken ct)
    {
        // agent_name here is the RECIPIENT — sending work to another agent is not acting as it,
        // so an OAuth-bound agent may address any agent of the same user.
        var agentId = await ResolveRecipientAgentId(caller, args, ct);
        if (agentId < 0) return Error("Agent not found");
        var req = new CreateHandoffRequest
        {
            Title = GetString(args, "title") ?? "",
            Prompt = GetString(args, "prompt") ?? ""
        };
        // the caller is recorded as the creator, so it can edit or delete the handoff until it's picked up
        var result = await handoffService.CreateAsync(agentId, req, caller.AgentId, ct);
        return result is null ? Error("Create failed") : Serialize(result, AppJsonSerializerContext.Default.HandoffCreated);
    }

    private async Task<string> HandlePickupHandoff(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var result = await handoffService.PickupAsync(agentId, GetInt(args, "handoff_id"), ct);
        return result is null ? Error("Handoff not found or already picked up") : Serialize(result, AppJsonSerializerContext.Default.HandoffPickedUp);
    }

    private async Task<string> HandleDeleteHandoff(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var handoffId = GetInt(args, "handoff_id");
        var r = await handoffService.DeleteAsync(agentId, handoffId, ct);
        return r.Status switch
        {
            "deleted" => "{\"handoff_id\":" + r.HandoffId + ",\"status\":\"deleted\",\"deleted_count\":1,\"title\":\"" + Esc(r.Title ?? "") + "\"}",
            "picked_up" => Error($"Handoff #{handoffId} was already picked up by {r.ToAgent} at {r.PickedUpAt:yyyy-MM-dd HH:mm} UTC. " +
                                 $"Only its recipient ({r.ToAgent}) can delete it now."),
            _ => Error(HandoffNotFound(handoffId))
        };
    }

    private async Task<string> HandleUpdateHandoff(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var handoffId = GetInt(args, "handoff_id");
        var title = GetString(args, "title");
        var prompt = GetString(args, "prompt");
        if (title is null && prompt is null) return Error("Nothing to change: pass a new title and/or prompt.");
        if (title is not null && title.Trim().Length == 0) return Error("The title can't be empty.");
        if (prompt is not null && prompt.Trim().Length == 0) return Error("The prompt can't be empty.");

        var r = await handoffService.UpdateAsync(agentId, handoffId, title, prompt, ct);
        return r.Status switch
        {
            "updated" => "{\"handoff_id\":" + r.HandoffId + ",\"status\":\"updated\",\"title\":\"" + Esc(r.Title ?? "") +
                         "\",\"to_agent\":\"" + Esc(r.ToAgent ?? "") + "\"}",
            "not_creator" => Error(r.FromAgent is null
                ? $"Handoff #{handoffId} has no recorded creator (it was created before creator tracking, or by an admin), " +
                  "so nobody can edit it. " + RecipientOptions(r)
                : $"Only the creator can edit handoff #{handoffId}; it was created by {r.FromAgent}. " + RecipientOptions(r)),
            "picked_up" => Error($"Handoff #{handoffId} was already picked up by {r.ToAgent} at {r.PickedUpAt:yyyy-MM-dd HH:mm} UTC, " +
                                 "so it can't be edited any more. Send a new handoff instead."),
            _ => Error(HandoffNotFound(handoffId))
        };
    }

    private async Task<string> HandleListSentHandoffs(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentId = ResolveAgentId(caller, args);
        if (agentId < 0) return Error("Agent not found");
        var list = await handoffService.ListSentAsync(agentId, GetBool(args, "pending_only"), ct);
        return Serialize(list, AppJsonSerializerContext.Default.HandoffSentList);
    }

    // what the recipient can still do with a handoff it can't edit
    private static string RecipientOptions(HandoffChange r) => r.PickedUpAt is null
        ? "As its recipient you can pick it up or delete it."
        : $"You picked it up at {r.PickedUpAt:yyyy-MM-dd HH:mm} UTC; as its recipient you can delete it.";

    private static string HandoffNotFound(int handoffId) =>
        $"Handoff #{handoffId} not found, or it's not one you created or received.";

    // ===============================================================
    //  HANDLERS: Images
    // ===============================================================

    private async Task<string> HandleGenerateImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new GenImageRequest
        {
            Prompt = GetString(args, "prompt") ?? "",
            Model = GetString(args, "model") ?? "nano-banana",
            AspectRatio = GetString(args, "aspect_ratio") ?? "1:1"
        };
        var result = await imageService.GenerateAsync(caller.UserId, caller.AgentId, req, ct);
        return Serialize(result, AppJsonSerializerContext.Default.ImageResponse);
    }

    private async Task<string> HandleEditImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new EditImageRequest
        {
            Prompt = GetString(args, "prompt") ?? "",
            ImageId = GetNullableInt(args, "image_id"),
            ImageUrl = GetString(args, "image_url"),
            Model = GetString(args, "model") ?? "nano-banana"
        };
        var result = await imageService.EditAsync(caller.UserId, caller.AgentId, req, ct);
        return Serialize(result, AppJsonSerializerContext.Default.ImageResponse);
    }

    private async Task<string> HandleUploadImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var data = GetString(args, "data");
        if (string.IsNullOrWhiteSpace(data)) return Error("data (base64 image) is required");

        // Accept a bare base64 string or a data: URL. The type always comes from the bytes, never from here.
        var comma = data.StartsWith("data:", StringComparison.Ordinal) ? data.IndexOf(',') : -1;
        var b64 = comma >= 0 ? data[(comma + 1)..] : data;

        // Refuse before decoding anything big: base64 is 4 chars per 3 bytes.
        const string tooBig = "Image is over the 25 MB upload limit";
        if ((long)b64.Length * 3 / 4 > ImageValidator.MaxUploadBytes + 3) return Error(tooBig);

        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64.Trim()); }
        catch (FormatException) { return Error("data is not valid base64"); }
        if (bytes.Length > ImageValidator.MaxUploadBytes) return Error(tooBig);

        var req = new UploadImageRequest
        {
            Data = bytes,
            Title = GetString(args, "title"),
            Description = GetString(args, "description"),
            Keep = GetBool(args, "keep", true)
        };
        var result = await imageService.UploadAsync(caller.UserId, caller.AgentId, req, ct);
        return Serialize(result, AppJsonSerializerContext.Default.ImageResponse);
    }

    private async Task<string> HandleAnalyzeImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var req = new AnalyzeImageRequest
        {
            ImageId = GetNullableInt(args, "image_id"),
            ImageUrl = GetString(args, "image_url"),
            Prompt = GetString(args, "prompt") ?? "Describe this image in detail"
        };
        var result = await imageService.AnalyzeAsync(caller.UserId, req, ct);
        return Serialize(result, AppJsonSerializerContext.Default.AnalyzeImageResponse);
    }

    private async Task<string> HandleListImages(Agent caller, JsonElement args, CancellationToken ct)
    {
        bool? keep = null;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("keep", out var keepProp) && keepProp.ValueKind != JsonValueKind.Null)
            keep = keepProp.GetBoolean();
        var limit = GetInt(args, "limit", 50);
        var offset = GetNullableInt(args, "offset") ?? 0;
        if (offset < 0) return Error("offset must be 0 or greater");
        var items = await imageService.ListAsync(caller.UserId, keep, limit, offset, ct);
        return "{\"images\":" + Serialize(items, AppJsonSerializerContext.Default.ListImageResponse) + "}";
    }

    private async Task<string> HandleGetImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var imageId = GetInt(args, "image_id");
        var result = await imageService.GetAsync(caller.UserId, imageId, ct);
        return result is null ? Error("Image not found") : Serialize(result, AppJsonSerializerContext.Default.ImageResponse);
    }

    private async Task<string> HandleKeepImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var imageId = GetInt(args, "image_id");
        var keep = GetBool(args, "keep", true);
        var result = await imageService.UpdateKeepAsync(caller.UserId, imageId, keep, ct);
        return result is null ? Error("Image not found") : Serialize(result, AppJsonSerializerContext.Default.ImageResponse);
    }

    private async Task<string> HandleDeleteImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var imageId = GetInt(args, "image_id");
        var force = GetBool(args, "force", false);
        var result = await imageService.DeleteAsync(caller.UserId, imageId, force, ct);
        if (result.Detail == "Not found") return Error("Image not found");
        return Serialize(result, AppJsonSerializerContext.Default.ImageDeleteResponse);
    }

    private async Task<string> HandleCleanupImages(Agent caller, CancellationToken ct)
    {
        var result = await imageService.CleanupAsync(caller.UserId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.ImageCleanupResponse);
    }

    // ===============================================================
    //  HANDLERS: Google Docs
    // ===============================================================

    private async Task<string> HandleCreateGoogleDoc(Agent caller, JsonElement args, CancellationToken ct)
    {
        var title = GetString(args, "title") ?? "";
        var body = GetString(args, "body");
        var result = await googleDocsService.CreateDocumentAsync(caller.UserId, title, body, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DocResponse);
    }

    private async Task<string> HandleGetDocImages(Agent caller, JsonElement args, CancellationToken ct)
    {
        var docId = GetString(args, "doc_id") ?? "";
        var result = await googleDocsService.GetDocImagesAsync(caller.UserId, docId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DocImagesResponse);
    }

    private async Task<string> HandleAppendDocImage(Agent caller, JsonElement args, CancellationToken ct)
    {
        var docId = GetString(args, "doc_id") ?? "";
        var imageId = GetInt(args, "image_id");
        double? widthPt = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("width_pt", out var w)
            && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : null;

        // Only the caller's own images. Docs fetches the public /nanoimages/ URL itself.
        var image = await imageService.GetAsync(caller.UserId, imageId, ct);
        if (image is null) return Error("Image not found");
        if (image.MimeType is not ("image/png" or "image/jpeg" or "image/gif"))
            return Error($"Google Docs can't embed {image.MimeType ?? "this image type"} (PNG, JPEG or GIF only)");
        if ((long)(image.Width ?? 0) * (image.Height ?? 0) > 25_000_000)
            return Error("Google Docs can't embed images over 25 megapixels");
        if (image.SizeBytes > 50 * 1024 * 1024)
            return Error("Google Docs can't embed images over 50 MB");

        var result = await googleDocsService.AppendImageAsync(caller.UserId, docId, image.Url, widthPt, image.Width, ct);
        result.ImageId = image.ImageId;
        result.ImageUrl = image.Url;
        return Serialize(result, AppJsonSerializerContext.Default.DocImageAppendResponse);
    }

    private async Task<string> HandleReadGoogleDoc(Agent caller, JsonElement args, CancellationToken ct)
    {
        var docId = GetString(args, "doc_id") ?? "";
        var result = await googleDocsService.ReadDocumentAsync(caller.UserId, docId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DocResponse);
    }

    private async Task<string> HandleUpdateGoogleDoc(Agent caller, JsonElement args, CancellationToken ct)
    {
        var docId = GetString(args, "doc_id") ?? "";
        var content = GetString(args, "content") ?? "";
        var result = await googleDocsService.UpdateDocumentAsync(caller.UserId, docId, content, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DocResponse);
    }

    private async Task<string> HandleAppendGoogleDoc(Agent caller, JsonElement args, CancellationToken ct)
    {
        var docId = GetString(args, "doc_id") ?? "";
        var content = GetString(args, "content") ?? "";
        var result = await googleDocsService.AppendToDocumentAsync(caller.UserId, docId, content, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DocResponse);
    }

    // ===============================================================
    //  HANDLERS: Google Drive
    // ===============================================================

    private async Task<string> HandleListGoogleFiles(Agent caller, JsonElement args, CancellationToken ct)
    {
        var folderId = GetString(args, "folder_id");
        var result = await googleDocsService.ListFilesAsync(caller.UserId, folderId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DriveFileListResponse);
    }

    private async Task<string> HandleCreateGoogleFolder(Agent caller, JsonElement args, CancellationToken ct)
    {
        var name = GetString(args, "name") ?? "";
        var parentId = GetString(args, "parent_folder_id");
        var result = await googleDocsService.CreateFolderAsync(caller.UserId, name, parentId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.FolderResponse);
    }

    private async Task<string> HandleMoveGoogleFile(Agent caller, JsonElement args, CancellationToken ct)
    {
        var fileId = GetString(args, "file_id") ?? "";
        var targetFolderId = GetString(args, "target_folder_id") ?? "";
        var result = await googleDocsService.MoveFileAsync(caller.UserId, fileId, targetFolderId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.MoveFileResponse);
    }

    private async Task<string> HandleDeleteGoogleFile(Agent caller, JsonElement args, CancellationToken ct)
    {
        var fileId = GetString(args, "file_id") ?? "";
        var result = await googleDocsService.DeleteFileAsync(caller.UserId, fileId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DeleteFileResponse);
    }

    private async Task<string> HandleGetGoogleFileMeta(Agent caller, JsonElement args, CancellationToken ct)
    {
        var fileId = GetString(args, "file_id") ?? "";
        var result = await googleDocsService.GetFileMetadataAsync(caller.UserId, fileId, ct);
        return Serialize(result, AppJsonSerializerContext.Default.DriveFileInfo);
    }

    // ===============================================================
    //  Helpers: Agent Resolution
    // ===============================================================

    // The token names exactly one agent, so agent_name may only name the caller (or be omitted):
    // acting as another agent, even one of the same user, is refused as "not found".
    private static int ResolveAgentId(Agent caller, JsonElement args)
    {
        var agentName = GetString(args, "agent_name");
        if (string.IsNullOrEmpty(agentName)) return caller.AgentId;
        return string.Equals(agentName, caller.AgentName, StringComparison.OrdinalIgnoreCase) ? caller.AgentId : -1;
    }

    // create_handoff only: the named agent is the recipient, which may be any agent of the same user.
    private async Task<int> ResolveRecipientAgentId(Agent caller, JsonElement args, CancellationToken ct)
    {
        var agentName = GetString(args, "agent_name");
        if (string.IsNullOrEmpty(agentName)) return caller.AgentId;
        var target = await agentService.GetByNameAsync(agentName, ct);
        if (target is null || target.UserId != caller.UserId) return -1;
        return target.AgentId;
    }

    private static (int AgentId, string AgentName) ResolveAgent(Agent caller, JsonElement args)
    {
        var agentName = GetString(args, "agent_name");
        if (string.IsNullOrEmpty(agentName)) return (caller.AgentId, caller.AgentName);
        return string.Equals(agentName, caller.AgentName, StringComparison.OrdinalIgnoreCase)
            ? (caller.AgentId, caller.AgentName) : (-1, "");
    }

    // ===============================================================
    //  Helpers: JSON argument extraction
    // ===============================================================

    private static string? GetString(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        return args.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() : null;
    }

    private static int GetInt(JsonElement args, string key, int defaultValue = 0)
    {
        if (args.ValueKind != JsonValueKind.Object) return defaultValue;
        if (!args.TryGetProperty(key, out var prop)) return defaultValue;
        return prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : defaultValue;
    }

    private static int? GetNullableInt(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty(key, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : null;
    }

    private static bool GetBool(JsonElement args, string key, bool defaultValue = false)
    {
        if (args.ValueKind != JsonValueKind.Object) return defaultValue;
        if (!args.TryGetProperty(key, out var prop)) return defaultValue;
        return prop.ValueKind is JsonValueKind.True or JsonValueKind.False ? prop.GetBoolean() : defaultValue;
    }

    private static string[]? GetStringArray(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty(key, out var prop) || prop.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                list.Add(item.GetString()!);
        }
        return list.ToArray();
    }

    // ===============================================================
    //  Helpers: Serialization
    // ===============================================================

    private static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.Serialize(value, typeInfo);
    }

    private static string Error(string message)
    {
        return "{\"error\":\"" + Esc(message) + "\"}";
    }

    private static string Esc(string s) => McpEndpoints.EscapeJsonString(s);

    // ===============================================================
    //  Tool Definitions (88 tools)
    // ===============================================================

    private static string BuildToolListJson()
    {
        // Reusable schema fragments
        const string _A = "\"agent_name\":{\"type\":\"string\",\"description\":\"Agent name (e.g. 'my-agent')\"}";
        const string _ID = "\"pkid\":{\"type\":\"integer\",\"description\":\"Node/item ID\"}";
        const string _T = "\"title\":{\"type\":\"string\",\"description\":\"Title\"}";
        const string _D = "\"description\":{\"type\":\"string\",\"description\":\"Description\"}";
        const string _PAR = "\"parent_id\":{\"type\":\"integer\",\"description\":\"Parent node ID (0 for root)\"}";
        const string _PJ = "\"project_id\":{\"type\":\"integer\",\"description\":\"Project ID\"}";
        const string _SC = "\"section_id\":{\"type\":\"integer\",\"description\":\"Section ID\"}";
        const string _FP = "\"file_path\":{\"type\":\"string\",\"description\":\"Associated file path\"}";
        const string _WK = "\"wiki_id\":{\"type\":\"integer\",\"description\":\"Wiki ID\"}";
        const string _TAGS = "\"tags\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"description\":\"Tags for the section\"}";
        const string _HID = "\"hint_id\":{\"type\":\"integer\",\"description\":\"Hint ID\"}";
        const string _SID = "\"share_id\":{\"type\":\"integer\",\"description\":\"Share ID\"}";
        const string _HOID = "\"handoff_id\":{\"type\":\"integer\",\"description\":\"Handoff ID\"}";

        var sb = new StringBuilder(16384);
        sb.Append("{\"tools\":[");
        var first = true;

        void Tool(string name, string desc, string propsJson, string reqJson)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append("{\"name\":\"").Append(name)
              .Append("\",\"description\":\"").Append(Esc(desc))
              .Append("\",\"inputSchema\":{\"type\":\"object\",\"properties\":{");
            // The token identifies the agent, so agent_name is optional (it defaults to, and may only name,
            // the bound agent). Exception: create_handoff's agent_name is the recipient, so it stays required.
            sb.Append(propsJson).Append("},\"required\":[");
            sb.Append(name == "create_handoff" ? reqJson : string.Join(',', reqJson.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(r => r.Trim() != "\"agent_name\"")));
            sb.Append("]}}");
        }

        // --- System ---
        Tool("get_time",
            "Get current UTC and Mountain Time timestamps, timezone, and day of week.",
            "", "");

        // --- Context ---
        Tool("get_context",
            "One-stop-shop agent startup context: time, always_load titles, memory titles, preferences manifest, project manifest, hints compact, and actionable nudges. " +
            "Each call opens a new session (returned as session: session_id, started_at, previous_started_at in UTC, previous_started_mountain); " +
            "describe it with set_session_description once its focus is clear, and again after a real change of direction.",
            _A, "\"agent_name\"");

        Tool("get_always_load",
            "Full always-load tree with descriptions.",
            _A, "\"agent_name\"");

        Tool("get_always_load_item",
            "Single always-load node and its children.",
            $"{_A},{_ID}", "\"agent_name\",\"pkid\"");

        Tool("create_always_load",
            "Create an always_load node.",
            $"{_A},{_T},{_D},{_PAR}", "\"agent_name\",\"title\"");

        Tool("update_always_load",
            "Update an always_load node.",
            $"{_A},{_ID},{_T},{_D}", "\"agent_name\",\"pkid\"");

        Tool("delete_always_load",
            "Delete an always_load node and children.",
            $"{_A},{_ID}", "\"agent_name\",\"pkid\"");

        // --- Memories ---
        Tool("get_memories",
            "All memories with full descriptions.",
            _A, "\"agent_name\"");

        Tool("get_memory",
            "Single memory by ID.",
            $"{_A},{_ID}", "\"agent_name\",\"pkid\"");

        Tool("create_memory",
            "Create a new memory.",
            $"{_A},{_T},{_D}", "\"agent_name\",\"title\"");

        Tool("update_memory",
            "Update a memory.",
            $"{_A},{_ID},{_T},{_D}", "\"agent_name\",\"pkid\"");

        Tool("delete_memory",
            "Delete a memory.",
            $"{_A},{_ID}", "\"agent_name\",\"pkid\"");

        // --- Preferences ---
        Tool("get_preferences",
            "Top-level preference categories.",
            _A, "\"agent_name\"");

        Tool("get_preference",
            "Preference node and its children.",
            $"{_A},{_ID}", "\"agent_name\",\"pkid\"");

        Tool("create_preference",
            "Create a preference node.",
            $"{_A},{_T},{_D},{_PAR}", "\"agent_name\",\"title\"");

        Tool("update_preference",
            "Update a preference node.",
            $"{_A},{_ID},{_T},{_D}", "\"agent_name\",\"pkid\"");

        Tool("delete_preference",
            "Delete a preference node and children.",
            $"{_A},{_ID}", "\"agent_name\",\"pkid\"");

        // --- Projects ---
        Tool("get_project_statuses",
            "List all project status options.",
            "", "");

        Tool("get_projects",
            "All projects for the user.",
            "\"status\":{\"type\":\"string\",\"description\":\"Filter by status code (planning, active, on_hold, evolving, complete, archived)\"}", "");

        Tool("get_project",
            "Project header with section tree.",
            _PJ, "\"project_id\"");

        Tool("get_project_compact",
            "Project header and section tree with titles only — no descriptions. Use to browse a project's structure before loading specific sections via get_section.",
            _PJ, "\"project_id\"");

        Tool("get_section",
            "Project section and its children.",
            $"{_PJ},{_SC}", "\"project_id\",\"section_id\"");

        Tool("create_project",
            "Create a new project.",
            $"{_T},{_D},\"status_id\":{{\"type\":\"integer\",\"description\":\"Status ID (default: 1=planning)\"}}", "\"title\"");

        Tool("create_section",
            "Create a section under a project.",
            $"{_PJ},{_T},{_D},{_PAR},{_FP}", "\"project_id\",\"title\"");

        Tool("update_project",
            "Update a project.",
            $"{_PJ},{_T},{_D},\"status_id\":{{\"type\":\"integer\",\"description\":\"Status ID for lifecycle transitions\"}}", "\"project_id\"");

        Tool("update_section",
            "Update a project section.",
            $"{_PJ},{_SC},{_T},{_D},{_FP}", "\"project_id\",\"section_id\"");

        Tool("delete_project",
            "Delete a project and all sections.",
            _PJ, "\"project_id\"");

        Tool("delete_section",
            "Delete a section and descendants.",
            $"{_PJ},{_SC}", "\"project_id\",\"section_id\"");

        // --- Hints ---
        Tool("get_hints",
            "Full hints tree with descriptions.",
            "", "");

        Tool("get_hints_compact",
            "Flat hint list with titles only — no descriptions. For quick category/hint discovery.",
            "", "");

        Tool("get_hint",
            "Hint node and its children.",
            _HID, "\"hint_id\"");

        Tool("create_hint_category",
            "Create a hint category. Omit parent_id (or pass 0) for a root category; pass parent_id to nest under an existing category.",
            $"{_PAR},{_T},{_D}", "\"title\"");

        Tool("create_hint",
            "Create a hint node.",
            $"{_T},{_D},{_PAR}", "\"title\"");

        Tool("update_hint",
            "Update a hint node.",
            $"{_HID},{_T},{_D}", "\"hint_id\"");

        Tool("delete_hint",
            "Delete a hint node and children.",
            _HID, "\"hint_id\"");

        Tool("delete_hint_category",
            "Delete a hint category. Fails if the category has any children — delete children first.",
            _HID, "\"hint_id\"");

        // --- Wikis ---
        Tool("get_wikis",
            "All wikis for the user.",
            "", "");

        Tool("get_wiki",
            "Wiki header with section tree (includes tags and updated_at). The wiki's document_url is a signed " +
            "link to its HTML document, valid 24 hours.",
            _WK, "\"wiki_id\"");

        Tool("create_wiki",
            "Create a new wiki.",
            $"{_T},{_D}", "\"title\"");

        Tool("update_wiki",
            "Update a wiki.",
            $"{_WK},{_T},{_D}", "\"wiki_id\"");

        Tool("delete_wiki",
            "Delete a wiki and all sections/tags.",
            _WK, "\"wiki_id\"");

        Tool("create_wiki_section",
            "Create a section under a wiki.",
            $"{_WK},{_T},{_D},{_PAR},{_TAGS}", "\"wiki_id\",\"title\"");

        Tool("get_wiki_section",
            "Wiki section with children and tags.",
            $"{_WK},{_SC}", "\"wiki_id\",\"section_id\"");

        Tool("update_wiki_section",
            "Update a wiki section (including tags).",
            $"{_WK},{_SC},{_T},{_D},{_TAGS}", "\"wiki_id\",\"section_id\"");

        Tool("delete_wiki_section",
            "Delete a wiki section and descendants.",
            $"{_WK},{_SC}", "\"wiki_id\",\"section_id\"");

        Tool("get_wiki_tags",
            "List all unique tags in a wiki.",
            _WK, "\"wiki_id\"");

        Tool("search_wiki_tag",
            "Find all sections across wikis matching a tag.",
            "\"tag\":{\"type\":\"string\",\"description\":\"Tag to search for\"}", "\"tag\"");

        // --- Sharing ---
        Tool("share_object",
            "Share a project, hint category, or wiki with another user.",
            "\"shared_to_user_id\":{\"type\":\"integer\",\"description\":\"User ID to share with\"}," +
            "\"object_type_id\":{\"type\":\"integer\",\"description\":\"1=project, 2=hint, 3=wiki\"}," +
            "\"object_id\":{\"type\":\"integer\",\"description\":\"ID of the object to share\"}," +
            "\"permission_level\":{\"type\":\"integer\",\"description\":\"1=read, 2=read+edit, 3=full control\"}",
            "\"shared_to_user_id\",\"object_type_id\",\"object_id\"");

        Tool("revoke_share",
            "Revoke a previously shared object.",
            _SID, "\"share_id\"");

        Tool("get_shared_by_me",
            "List all objects you have shared with other users.",
            "", "");

        Tool("get_shared_to_me",
            "List all objects other users have shared with you.",
            "", "");

        Tool("check_access",
            "Check your access to an object: has_access and permission_level " +
            "(3 = owner or admin share, 2 = read-write, 1 = read-only, 0 = none).",
            "\"object_type_id\":{\"type\":\"integer\",\"description\":\"1=project, 2=hint category (root hint ID), 3=wiki\"}," +
            "\"object_id\":{\"type\":\"integer\",\"description\":\"ID of the object to check\"}",
            "\"object_type_id\",\"object_id\"");

        // --- Sessions ---
        Tool("set_session_description",
            "Describe what your current session (opened by get_context) is about, in a short phrase. Call it once the " +
            "session's focus is clear, and again whenever the focus changes substantially (a real change of direction, " +
            "not every topic shift). Each call replaces the previous description. Rick sees it in LucyAdmin next to the " +
            "projects you loaded.",
            "\"description\":{\"type\":\"string\",\"description\":\"Short description of the session's work (max 500 characters)\"}",
            "\"description\"");

        // --- Save Notes ---
        Tool("save_notes",
            "Email markdown content as an attachment to Rick.",
            "\"subject\":{\"type\":\"string\",\"description\":\"Email subject\"}," +
            "\"content\":{\"type\":\"string\",\"description\":\"Markdown content\"}",
            "\"subject\",\"content\"");

        // --- Nudges ---
        const string _NID = "\"nudge_id\":{\"type\":\"integer\",\"description\":\"Nudge ID\"}";

        Tool("get_nudges",
            "List all nudges for the current user.",
            "", "");

        Tool("get_nudge",
            "Get a single nudge by ID.",
            _NID, "\"nudge_id\"");

        Tool("create_nudge",
            "Create a new nudge (deadline reminder). Set due_date to a date string (YYYY-MM-DD) or omit for ASAP.",
            $"{_T},{_D},\"due_date\":{{\"type\":\"string\",\"description\":\"Due date (YYYY-MM-DD) or omit for ASAP\"}}",
            "\"title\"");

        Tool("update_nudge",
            "Update a nudge. Set clear_due_date=true to change to ASAP.",
            $"{_NID},{_T},{_D},\"due_date\":{{\"type\":\"string\",\"description\":\"Due date (YYYY-MM-DD)\"}},\"clear_due_date\":{{\"type\":\"boolean\",\"description\":\"Set true to clear due date (makes it ASAP)\"}}",
            "\"nudge_id\"");

        Tool("delete_nudge",
            "Delete a nudge.",
            _NID, "\"nudge_id\"");

        // --- Handoffs ---
        Tool("list_handoffs",
            "List pending handoffs addressed to you. from_agent names the sender (null if unknown); " +
            "updated_at is set if the sender edited it (null = never edited).",
            _A, "\"agent_name\"");

        Tool("get_handoff",
            "Get a handoff you received or created, picked up or not. updated_at is the sender's last edit (null = never edited).",
            $"{_A},{_HOID}", "\"agent_name\",\"handoff_id\"");

        Tool("create_handoff",
            "Create a handoff prompt for an agent (agent_name is the RECIPIENT; cross-agent OK). You're recorded as its creator, " +
            "so you can edit (update_handoff) or delete it until it's picked up.",
            $"{_A},{_T},\"prompt\":{{\"type\":\"string\",\"description\":\"Handoff prompt text\"}}",
            "\"agent_name\",\"title\",\"prompt\"");

        Tool("pickup_handoff",
            "Mark a handoff as picked up (named agent only).",
            $"{_A},{_HOID}", "\"agent_name\",\"handoff_id\"");

        Tool("delete_handoff",
            "Delete a handoff. As its recipient: any time. As its creator: only while it's still pending (not picked up). " +
            "If you can't, the error says why.",
            $"{_A},{_HOID}", "\"agent_name\",\"handoff_id\"");

        Tool("update_handoff",
            "Edit a handoff you created, while it's still pending (not picked up). Pass a new title and/or prompt; " +
            "anything omitted stays as it is. If you can't, the error says why. Pending isn't unread: the recipient may " +
            "already have read it, so the edit sets updated_at (shown to the recipient); for a material change, tell them too.",
            $"{_A},{_HOID},{_T},\"prompt\":{{\"type\":\"string\",\"description\":\"New handoff prompt text\"}}",
            "\"agent_name\",\"handoff_id\"");

        Tool("list_sent_handoffs",
            "List handoffs you created, newest first, with the recipient, pickup time and last edit. At most 100: " +
            "truncated=true means there are older ones not shown. pending_only=true shows only those not yet picked up.",
            $"{_A},\"pending_only\":{{\"type\":\"boolean\",\"description\":\"Only handoffs not yet picked up (default false)\"}}",
            "\"agent_name\"");

        // --- Images ---
        Tool("generate_image",
            "Generate an image from a text prompt via Gemini.",
            "\"prompt\":{\"type\":\"string\",\"description\":\"Text prompt for image generation\"}," +
            "\"model\":{\"type\":\"string\",\"description\":\"Model alias (nano-banana or nano-banana-pro)\"}," +
            "\"aspect_ratio\":{\"type\":\"string\",\"description\":\"Aspect ratio (1:1, 16:9, etc.)\"}",
            "\"prompt\"");

        Tool("edit_image",
            "Edit an existing image with a text prompt via Gemini.",
            "\"prompt\":{\"type\":\"string\",\"description\":\"Edit instruction\"}," +
            "\"image_id\":{\"type\":\"integer\",\"description\":\"Source image ID (provide this or image_url)\"}," +
            "\"image_url\":{\"type\":\"string\",\"description\":\"Source image URL (provide this or image_id)\"}," +
            "\"model\":{\"type\":\"string\",\"description\":\"Model alias\"}",
            "\"prompt\"");

        Tool("upload_image",
            "Upload an image (PNG, JPEG, WebP or GIF; max 25 MB) and get a permanent public URL. " +
            "Images are PUBLIC: anyone with the URL can view them. WebP is stored as PNG (animated WebP: first frame only); " +
            "the response's notice says when that happened.",
            "\"data\":{\"type\":\"string\",\"description\":\"Image bytes, base64 (a data: URL is also accepted)\"}," +
            "\"title\":{\"type\":\"string\",\"description\":\"Short title\"}," +
            "\"description\":{\"type\":\"string\",\"description\":\"Optional description\"}," +
            "\"keep\":{\"type\":\"boolean\",\"description\":\"Protect from cleanup_images (default true)\"}",
            "\"data\",\"title\"");

        Tool("analyze_image",
            "Analyze an image and return a text description via Gemini.",
            "\"image_id\":{\"type\":\"integer\",\"description\":\"Image ID (provide this or image_url)\"}," +
            "\"image_url\":{\"type\":\"string\",\"description\":\"Image URL (provide this or image_id)\"}," +
            "\"prompt\":{\"type\":\"string\",\"description\":\"Analysis prompt\"}",
            "");

        Tool("list_images",
            "List generated images with optional keep filter.",
            "\"keep\":{\"type\":\"boolean\",\"description\":\"Filter by keep flag\"}," +
            "\"limit\":{\"type\":\"integer\",\"description\":\"Max results (default 50)\"}," +
            "\"offset\":{\"type\":\"integer\",\"description\":\"Results to skip, for paging (default 0)\"}",
            "");

        Tool("get_image",
            "Get one of your images by ID (same fields as list_images).",
            "\"image_id\":{\"type\":\"integer\",\"description\":\"Image ID\"}", "\"image_id\"");

        Tool("keep_image",
            "Set an image's keep flag: keep=true (default) protects it from cleanup_images, keep=false releases it.",
            "\"image_id\":{\"type\":\"integer\",\"description\":\"Image ID\"}," +
            "\"keep\":{\"type\":\"boolean\",\"description\":\"Keep flag to set (default true)\"}",
            "\"image_id\"");

        Tool("delete_image",
            "Delete an image file and DB row.",
            "\"image_id\":{\"type\":\"integer\",\"description\":\"Image ID to delete\"}," +
            "\"force\":{\"type\":\"boolean\",\"description\":\"Force delete even if keep=true\"}",
            "\"image_id\"");

        Tool("cleanup_images",
            "Bulk delete all images where keep=false.",
            "", "");

        // --- Google Docs ---
        Tool("create_google_doc",
            "Create a new Google Doc in the shared Drive folder.",
            "\"title\":{\"type\":\"string\",\"description\":\"Document title\"}," +
            "\"body\":{\"type\":\"string\",\"description\":\"Plain text body content\"}",
            "\"title\"");

        Tool("read_google_doc",
            "Read a Google Doc and return its plain text content.",
            "\"doc_id\":{\"type\":\"string\",\"description\":\"Google Doc document ID\"}", "\"doc_id\"");

        Tool("get_doc_images",
            "List the inline images in a Google Doc, in document order: content_uri (a short-lived Google link: " +
            "use it right away, e.g. with analyze_image, and never store it), source_uri, size in points, " +
            "position (start_index) and the surrounding text. read_google_doc returns text only.",
            "\"doc_id\":{\"type\":\"string\",\"description\":\"Google Doc document ID\"}", "\"doc_id\"");

        Tool("append_doc_image",
            "Append one of your images (by image_id, from upload_image / generate_image / list_images) to the end " +
            "of a Google Doc, on its own line. PNG, JPEG or GIF. The image never extends past the margins: any width " +
            "(width_pt or the natural size) wider than the doc's text width is reduced to fit. The response's width_pt " +
            "is the width actually used (null = natural size).",
            "\"doc_id\":{\"type\":\"string\",\"description\":\"Google Doc document ID\"}," +
            "\"image_id\":{\"type\":\"integer\",\"description\":\"Your image's ID\"}," +
            "\"width_pt\":{\"type\":\"number\",\"description\":\"Optional width in points; height keeps the aspect ratio. " +
            "The text width depends on the doc's margins (468pt on Letter with 1-inch margins, 540pt with 0.5-inch). " +
            "Omit to use the natural size, fitted to the page. Values wider than the text width are clamped to it.\"}",
            "\"doc_id\",\"image_id\"");

        Tool("update_google_doc",
            "Replace a document's content with new text.",
            "\"doc_id\":{\"type\":\"string\",\"description\":\"Google Doc document ID\"}," +
            "\"content\":{\"type\":\"string\",\"description\":\"Replacement text content\"}",
            "\"doc_id\",\"content\"");

        Tool("append_google_doc",
            "Append text content to an existing document.",
            "\"doc_id\":{\"type\":\"string\",\"description\":\"Google Doc document ID\"}," +
            "\"content\":{\"type\":\"string\",\"description\":\"Text content to append\"}",
            "\"doc_id\",\"content\"");

        // --- Google Drive ---
        Tool("list_google_files",
            "List files in the shared Drive folder or a subfolder.",
            "\"folder_id\":{\"type\":\"string\",\"description\":\"Subfolder ID (defaults to root shared folder)\"}", "");

        Tool("create_google_folder",
            "Create a subfolder in the shared Drive folder.",
            "\"name\":{\"type\":\"string\",\"description\":\"Folder name\"}," +
            "\"parent_folder_id\":{\"type\":\"string\",\"description\":\"Parent folder ID\"}",
            "\"name\"");

        Tool("move_google_file",
            "Move a file to a different folder.",
            "\"file_id\":{\"type\":\"string\",\"description\":\"File ID to move\"}," +
            "\"target_folder_id\":{\"type\":\"string\",\"description\":\"Destination folder ID\"}",
            "\"file_id\",\"target_folder_id\"");

        Tool("delete_google_file",
            "Move a file to trash.",
            "\"file_id\":{\"type\":\"string\",\"description\":\"File ID to delete\"}", "\"file_id\"");

        Tool("get_google_file_meta",
            "Get file metadata (title, type, dates, URL).",
            "\"file_id\":{\"type\":\"string\",\"description\":\"File ID\"}", "\"file_id\"");

        sb.Append("]}");
        return sb.ToString();
    }
}
