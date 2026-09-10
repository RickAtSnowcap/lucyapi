using LucyAPI.Api.Auth;
using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;
using Microsoft.Extensions.Configuration;

namespace LucyAPI.Api.Endpoints.Admin;

public static class AdminResourcesEndpoints
{
    public static void MapAdminResourcesEndpoints(this WebApplication app)
    {
        // ── Project Statuses ─────────────────────────────────────

        app.MapGet("/admin/project-statuses", async (
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var statuses = await projectService.GetStatusesAsync(ct);
            return Results.Ok(new AdminProjectStatusListResponse { Statuses = statuses });
        });

        // ── Projects ─────────────────────────────────────────────

        app.MapGet("/admin/projects", async (
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var projects = await projectService.GetAllAsync(caller.UserId, null, ct);
            return Results.Ok(projects);
        });

        app.MapGet("/admin/projects/{projectId:int}", async (
            int projectId,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            IAgentService agentService,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var sections = await sectionService.GetSectionsAsync(projectId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);

            var agentKey = await agentService.GetFirstKeyByUserIdAsync(caller.UserId, ct);
            if (agentKey is not null)
            {
                var baseUrl = config["Images:BaseUrl"] ?? "";
                project.DocumentUrl = $"{baseUrl}/projects/{projectId}/document?agent_key={agentKey}";
            }

            return Results.Ok(new ProjectDetailResponse { Project = project, Sections = tree });
        });

        app.MapPost("/admin/projects", async (
            CreateProjectRequest request,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await projectService.CreateAsync(caller.UserId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<ProjectCreated> { Created = result });
        });

        app.MapPut("/admin/projects/{projectId:int}", async (
            int projectId,
            UpdateProjectRequest request,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            // Verify access (owned or shared with edit permission)
            var existing = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (existing is null) return Results.NotFound();

            var result = await projectService.UpdateAsync(projectId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new AdminUpdatedResponse<ProjectCreated> { Updated = result });
        });

        app.MapDelete("/admin/projects/{projectId:int}", async (
            int projectId,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            // Only owner can delete — verify ownership
            var existing = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (existing is null) return Results.NotFound();

            var count = await projectService.DeleteAsync(projectId, ct);
            return Results.Ok(new AdminSectionsDeletedResponse { Deleted = projectId, SectionsDeleted = count });
        });

        // ── Project Sections ─────────────────────────────────────

        app.MapPost("/admin/projects/{projectId:int}/sections", async (
            int projectId,
            CreateSectionRequest request,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var result = await sectionService.CreateAsync(projectId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<SectionCreated> { Created = result });
        });

        app.MapPut("/admin/projects/{projectId:int}/sections/{sectionId:int}", async (
            int projectId,
            int sectionId,
            UpdateSectionRequest request,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var result = await sectionService.UpdateAsync(projectId, sectionId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new AdminUpdatedResponse<SectionCreated> { Updated = result });
        });

        app.MapDelete("/admin/projects/{projectId:int}/sections/{sectionId:int}", async (
            int projectId,
            int sectionId,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var count = await sectionService.DeleteAsync(projectId, sectionId, ct);
            if (count == 0) return Results.NotFound();
            return Results.Ok(new AdminDeletedResponse { Deleted = sectionId, DescendantsDeleted = count - 1 });
        });

        // ── Wikis ────────────────────────────────────────────────

        app.MapGet("/admin/wikis", async (
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var wikis = await wikiService.GetAllAsync(caller.UserId, ct);
            return Results.Ok(wikis);
        });

        app.MapGet("/admin/wikis/{wikiId:int}/tags", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiTagService wikiTagService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var tags = await wikiTagService.GetTagsAsync(wikiId, ct);
            return Results.Ok(new AdminWikiTagListResponse { WikiId = wikiId, Tags = tags });
        });

        app.MapGet("/admin/wikis/{wikiId:int}", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var sections = await wikiSectionService.GetSectionsAsync(wikiId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            return Results.Ok(new WikiDetailResponse { Wiki = wiki, Sections = tree });
        });

        app.MapPost("/admin/wikis", async (
            CreateWikiRequest request,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await wikiService.CreateAsync(caller.UserId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<WikiCreated> { Created = result });
        });

        app.MapPut("/admin/wikis/{wikiId:int}", async (
            int wikiId,
            UpdateWikiRequest request,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var existing = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (existing is null) return Results.NotFound();

            var result = await wikiService.UpdateAsync(wikiId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new AdminUpdatedResponse<WikiCreated> { Updated = result });
        });

        app.MapDelete("/admin/wikis/{wikiId:int}", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var existing = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (existing is null) return Results.NotFound();

            var count = await wikiService.DeleteAsync(wikiId, ct);
            return Results.Ok(new AdminSectionsDeletedResponse { Deleted = wikiId, SectionsDeleted = count });
        });

        // ── Wiki Sections ────────────────────────────────────────

        app.MapPost("/admin/wikis/{wikiId:int}/sections", async (
            int wikiId,
            CreateWikiSectionRequest request,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var result = await wikiSectionService.CreateAsync(wikiId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<WikiSectionCreated> { Created = result });
        });

        app.MapPut("/admin/wikis/{wikiId:int}/sections/{sectionId:int}", async (
            int wikiId,
            int sectionId,
            UpdateWikiSectionRequest request,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var result = await wikiSectionService.UpdateAsync(wikiId, sectionId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new AdminUpdatedResponse<WikiSectionCreated> { Updated = result });
        });

        app.MapDelete("/admin/wikis/{wikiId:int}/sections/{sectionId:int}", async (
            int wikiId,
            int sectionId,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var count = await wikiSectionService.DeleteAsync(wikiId, sectionId, ct);
            if (count == 0) return Results.NotFound();
            return Results.Ok(new AdminDeletedResponse { Deleted = sectionId, DescendantsDeleted = count - 1 });
        });

        // ── Hints ────────────────────────────────────────────────

        app.MapGet("/admin/hints", async (
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var hints = await hintService.GetAllAsync(caller.UserId, ct);
            return Results.Ok(hints);
        });

        app.MapGet("/admin/hints/{hintId:int}", async (
            int hintId,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var items = await hintService.GetAsync(hintId, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/admin/hint-categories", async (
            CreateHintCategoryRequest request,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await hintService.CreateCategoryAsync(caller.UserId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<HintCreated> { Created = result });
        });

        app.MapPost("/admin/hints", async (
            CreateHintRequest request,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await hintService.CreateAsync(caller.UserId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<HintCreated> { Created = result });
        });

        app.MapPut("/admin/hints/{hintId:int}", async (
            int hintId,
            UpdateHintRequest request,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var result = await hintService.UpdateAsync(hintId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new AdminUpdatedResponse<MutationResult> { Updated = result });
        });

        app.MapDelete("/admin/hints/{hintId:int}", async (
            int hintId,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var count = await hintService.DeleteAsync(hintId, ct);
            if (count == 0) return Results.NotFound();
            return Results.Ok(new AdminDeletedResponse { Deleted = hintId, DescendantsDeleted = count - 1 });
        });

        // ── Secrets ──────────────────────────────────────────────

        app.MapGet("/admin/secrets", async (
            HttpContext ctx,
            AdminRepository adminRepo,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var rows = await adminRepo.ListSecretKeysAsync(caller.UserId, ct);
            var secrets = rows.Select(r => new AdminSecretKey
            {
                Key = r.Key,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();
            return Results.Ok(new AdminSecretListResponse { Secrets = secrets });
        });

        app.MapGet("/admin/secrets/{key}", async (
            string key,
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var secret = await secretService.GetAsync(caller.UserId, key, ct);
            if (secret is null) return Results.NotFound();
            return Results.Ok(new AdminSecretValueResponse { Key = key, Value = secret.Value ?? "" });
        });

        app.MapPut("/admin/secrets/{key}", async (
            string key,
            SetSecretRequest request,
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            await secretService.SetAsync(caller.UserId, key, request, ct);
            return Results.Ok(new KeyStatusResponse { Key = key, Status = "saved" });
        });

        app.MapDelete("/admin/secrets/{key}", async (
            string key,
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var deleted = await secretService.DeleteAsync(caller.UserId, key, ct);
            return deleted
                ? Results.Ok(new AdminDeletedKeyResponse { Deleted = key })
                : Results.NotFound();
        });

        // ── Nudges ───────────────────────────────────────────────

        app.MapGet("/admin/nudges", async (
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var nudges = await nudgeService.GetAllAsync(caller.UserId, ct);
            return Results.Ok(nudges);
        });

        app.MapGet("/admin/nudges/{nudgeId:int}", async (
            int nudgeId,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var nudge = await nudgeService.GetOneAsync(caller.UserId, nudgeId, ct);
            return nudge is null ? Results.NotFound() : Results.Ok(nudge);
        });

        app.MapPost("/admin/nudges", async (
            CreateNudgeRequest request,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await nudgeService.CreateAsync(caller.UserId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Ok(new AdminCreatedResponse<NudgeCreated> { Created = result });
        });

        app.MapPut("/admin/nudges/{nudgeId:int}", async (
            int nudgeId,
            UpdateNudgeRequest request,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await nudgeService.UpdateAsync(caller.UserId, nudgeId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new AdminUpdatedResponse<MutationResult> { Updated = result });
        });

        app.MapDelete("/admin/nudges/{nudgeId:int}", async (
            int nudgeId,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var count = await nudgeService.DeleteAsync(caller.UserId, nudgeId, ct);
            return count == 0 ? Results.NotFound() : Results.Ok(new AdminDeletedIdResponse { Deleted = nudgeId });
        });

        // ── Users ────────────────────────────────────────────────

        app.MapGet("/admin/users", async (
            HttpContext ctx,
            IUserService userService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var users = await userService.ListOtherUsersAsync(caller.UserId, ct);
            var items = users.Select(u => new UserInfoResponse
            {
                UserId = u.UserId,
                Name = u.Name,
                Username = u.Username,
                Email = u.Email
            }).ToList();
            return Results.Ok(new AdminUserListResponse { Users = items });
        });

        // ── Sharing ──────────────────────────────────────────────

        app.MapGet("/admin/sharing/by-me", async (
            HttpContext ctx,
            AdminRepository adminRepo,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var rows = await adminRepo.ListSharesByMeAsync(caller.UserId, ct);
            var shares = rows.Select(r => new AdminShareItem
            {
                ShareId = r.ShareId,
                ObjectTypeId = r.ObjectTypeId,
                ObjectType = r.ObjectType,
                ObjectId = r.ObjectId,
                SharedToUserId = r.SharedToUserId,
                SharedToName = r.SharedToName,
                PermissionLevel = r.PermissionLevel,
                ObjectTitle = r.ObjectTitle
            }).ToList();
            return Results.Ok(new AdminShareListResponse { Shares = shares });
        });

        app.MapGet("/admin/sharing/to-me", async (
            HttpContext ctx,
            AdminRepository adminRepo,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var rows = await adminRepo.ListSharesToMeAsync(caller.UserId, ct);
            var shares = rows.Select(r => new AdminShareItem
            {
                ShareId = r.ShareId,
                ObjectTypeId = r.ObjectTypeId,
                ObjectType = r.ObjectType,
                ObjectId = r.ObjectId,
                SharedByUserId = r.SharedByUserId,
                SharedByName = r.SharedByName,
                PermissionLevel = r.PermissionLevel,
                ObjectTitle = r.ObjectTitle
            }).ToList();
            return Results.Ok(new AdminShareListResponse { Shares = shares });
        });

        app.MapPost("/admin/sharing", async (
            ShareObjectRequest request,
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await shareService.ShareAsync(caller.UserId, request, ct);
            if (result is null) return Results.BadRequest();
            return Results.Ok(new AdminShareCreatedResponse
            {
                Shared = new AdminShareCreatedItem
                {
                    ShareId = result.ShareId,
                    ObjectTypeId = result.ObjectTypeId,
                    ObjectId = result.ObjectId,
                    SharedToUserId = result.SharedToUserId,
                    PermissionLevel = result.PermissionLevel
                }
            });
        });

        app.MapPut("/admin/sharing/{shareId:int}", async (
            int shareId,
            AdminShareUpdateRequest request,
            HttpContext ctx,
            AdminRepository adminRepo,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            if (request.PermissionLevel < 1 || request.PermissionLevel > 3)
                return Results.BadRequest(new ErrorResponse { Error = "permission_level must be 1, 2, or 3" });

            var result = await adminRepo.UpdateSharePermissionAsync(shareId, caller.UserId, request.PermissionLevel, ct);
            if (result is null)
                return Results.NotFound(new ErrorResponse { Error = "Share not found or you are not the owner" });

            return Results.Ok(new AdminUpdatedResponse<AdminShareCreatedItem>
            {
                Updated = new AdminShareCreatedItem
                {
                    ShareId = result.Value.ShareId,
                    ObjectTypeId = result.Value.ObjectTypeId,
                    ObjectId = result.Value.ObjectId,
                    SharedToUserId = result.Value.SharedToUserId,
                    PermissionLevel = result.Value.PermissionLevel
                }
            });
        });

        app.MapDelete("/admin/sharing/{shareId:int}", async (
            int shareId,
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var revoked = await shareService.RevokeAsync(caller.UserId, shareId, ct);
            return revoked
                ? Results.Ok(new AdminRevokedResponse { Revoked = shareId })
                : Results.NotFound();
        });

        // ── Images ───────────────────────────────────────────────

        app.MapGet("/admin/images", async (
            bool? keep,
            int? limit,
            int? offset,
            HttpContext ctx,
            IImageService imageService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var images = await imageService.ListAsync(caller.UserId, keep, limit ?? 50, offset ?? 0, ct);
            return Results.Ok(images);
        });

        app.MapGet("/admin/images/{imageId:int}", async (
            int imageId,
            HttpContext ctx,
            IImageService imageService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var image = await imageService.GetAsync(imageId, ct);
            return image is null ? Results.NotFound() : Results.Ok(image);
        });

        app.MapPost("/admin/images/generate", async (
            GenImageRequest request,
            HttpContext ctx,
            IImageService imageService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await imageService.GenerateAsync(caller.UserId, request, ct);
            return Results.Ok(result);
        });

        app.MapPatch("/admin/images/{imageId:int}", async (
            int imageId,
            AdminKeepRequest request,
            HttpContext ctx,
            IImageService imageService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var result = await imageService.UpdateKeepAsync(imageId, request.Keep, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/admin/images/{imageId:int}", async (
            int imageId,
            bool? force,
            HttpContext ctx,
            IImageService imageService,
            CancellationToken ct) =>
        {
            _ = ctx.GetUserContext();
            var result = await imageService.DeleteAsync(imageId, force ?? false, ct);
            return Results.Ok(result);
        });

        app.MapPost("/admin/images/cleanup", async (
            HttpContext ctx,
            IImageService imageService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var result = await imageService.CleanupAsync(caller.UserId, ct);
            return Results.Ok(result);
        });

        // ── Dashboard ────────────────────────────────────────────

        app.MapGet("/admin/dashboard", async (
            HttpContext ctx,
            AdminRepository adminRepo,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var stats = await adminRepo.GetDashboardStatsAsync(caller.UserId, ct);
            var sessions = await adminRepo.GetRecentSessionsAsync(caller.UserId, 5, ct);

            return Results.Ok(new AdminDashboardResponse
            {
                Stats = new AdminDashboardStats
                {
                    Agents = stats.Agents,
                    Projects = stats.Projects,
                    Wikis = stats.Wikis,
                    HintCategories = stats.HintCategories,
                    Secrets = stats.Secrets,
                    Images = stats.Images,
                    PendingHandoffs = stats.PendingHandoffs,
                    SharedToMe = stats.SharedToMe
                },
                RecentSessions = sessions.Select(s => new AdminRecentSession
                {
                    SessionId = s.SessionId,
                    AgentName = s.AgentName,
                    StartedAt = s.StartedAt?.ToString("o"),
                    Project = s.Project
                }).ToList()
            });
        });
    }
}
