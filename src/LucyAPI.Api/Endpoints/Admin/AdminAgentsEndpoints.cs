using LucyAPI.Api.Auth;
using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints.Admin;

public static class AdminAgentsEndpoints
{
    public static void MapAdminAgentsEndpoints(this WebApplication app)
    {
        // ── Agents list ──────────────────────────────────────────

        app.MapGet("/admin/agents", async (
            HttpContext ctx,
            AdminRepository adminRepo,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var rows = await adminRepo.ListAgentsWithLastSessionAsync(caller.UserId, ct);

            var agents = rows.Select(r => new AdminAgentItem
            {
                AgentId = r.AgentId,
                Name = r.Name,
                LastSession = r.SessionId.HasValue ? new AdminLastSession
                {
                    SessionId = r.SessionId.Value,
                    StartedAt = r.StartedAt?.ToString("o"),
                    Project = r.Project
                } : null
            }).ToList();

            return Results.Ok(new AdminAgentListResponse { Agents = agents });
        });

        // ── Always Load ──────────────────────────────────────────

        app.MapGet("/admin/agents/{agentName}/always-load", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var items = await alwaysLoadService.GetAllAsync(agent.Value.AgentId, ct);
            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId);
            return Results.Ok(new AgentAlwaysLoadListResponse { Agent = agentName, AlwaysLoad = tree });
        });

        app.MapGet("/admin/agents/{agentName}/always-load/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var items = await alwaysLoadService.GetItemAsync(agent.Value.AgentId, pkid, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/admin/agents/{agentName}/always-load", async (
            string agentName,
            CreateAlwaysLoadRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await alwaysLoadService.CreateAsync(agent.Value.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Ok(new AdminCreatedResponse<AlwaysLoadCreated> { Created = result });
        });

        app.MapPut("/admin/agents/{agentName}/always-load/{pkid:int}", async (
            string agentName,
            int pkid,
            UpdateAlwaysLoadRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await alwaysLoadService.UpdateAsync(agent.Value.AgentId, pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(new AdminUpdatedResponse<MutationResult> { Updated = result });
        });

        app.MapDelete("/admin/agents/{agentName}/always-load/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var count = await alwaysLoadService.DeleteAsync(agent.Value.AgentId, pkid, ct);
            if (count == 0) return Results.NotFound();
            return Results.Ok(new AdminDeletedResponse { Deleted = pkid, DescendantsDeleted = count - 1 });
        });

        // ── Memories ─────────────────────────────────────────────

        app.MapGet("/admin/agents/{agentName}/memories", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var memories = await memoryService.GetAllAsync(agent.Value.AgentId, ct);
            return Results.Ok(new AdminMemoryListResponse { Agent = agentName, Memories = memories });
        });

        app.MapGet("/admin/agents/{agentName}/memories/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var memory = await memoryService.GetOneAsync(agent.Value.AgentId, pkid, ct);
            return memory is null ? Results.NotFound() : Results.Ok(memory);
        });

        app.MapPost("/admin/agents/{agentName}/memories", async (
            string agentName,
            CreateMemoryRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await memoryService.CreateAsync(agent.Value.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Ok(new AdminCreatedResponse<MemoryCreated> { Created = result });
        });

        app.MapPut("/admin/agents/{agentName}/memories/{pkid:int}", async (
            string agentName,
            int pkid,
            UpdateMemoryRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await memoryService.UpdateAsync(agent.Value.AgentId, pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(new AdminUpdatedResponse<MutationResult> { Updated = result });
        });

        app.MapDelete("/admin/agents/{agentName}/memories/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var count = await memoryService.DeleteAsync(agent.Value.AgentId, pkid, ct);
            return count > 0 ? Results.Ok(new AdminDeletedIdResponse { Deleted = pkid }) : Results.NotFound();
        });

        // ── Preferences ──────────────────────────────────────────

        app.MapGet("/admin/agents/{agentName}/preferences", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var items = await preferenceService.GetAllAsync(agent.Value.AgentId, ct);
            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId);
            return Results.Ok(new AgentPreferenceTreeResponse { Agent = agentName, Preferences = tree });
        });

        app.MapGet("/admin/agents/{agentName}/preferences/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var items = await preferenceService.GetBranchAsync(agent.Value.AgentId, pkid, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/admin/agents/{agentName}/preferences", async (
            string agentName,
            CreatePreferenceRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await preferenceService.CreateAsync(agent.Value.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Ok(new AdminCreatedResponse<MutationResult> { Created = result });
        });

        app.MapPut("/admin/agents/{agentName}/preferences/{pkid:int}", async (
            string agentName,
            int pkid,
            UpdatePreferenceRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await preferenceService.UpdateAsync(agent.Value.AgentId, pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(new AdminUpdatedResponse<MutationResult> { Updated = result });
        });

        app.MapDelete("/admin/agents/{agentName}/preferences/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var count = await preferenceService.DeleteAsync(agent.Value.AgentId, pkid, ct);
            if (count == 0) return Results.NotFound();
            return Results.Ok(new AdminDeletedResponse { Deleted = pkid, DescendantsDeleted = count - 1 });
        });

        // ── Handoffs ─────────────────────────────────────────────

        app.MapGet("/admin/agents/{agentName}/handoffs", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var handoffs = await handoffService.ListPendingAsync(agent.Value.AgentId, ct);
            return Results.Ok(new AdminHandoffListResponse { Agent = agentName, Handoffs = handoffs });
        });

        app.MapGet("/admin/agents/{agentName}/handoffs/{handoffId:int}", async (
            string agentName,
            int handoffId,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var handoff = await handoffService.GetAsync(agent.Value.AgentId, handoffId, ct);
            return handoff is null ? Results.NotFound() : Results.Ok(handoff);
        });

        app.MapPost("/admin/agents/{agentName}/handoffs", async (
            string agentName,
            CreateHandoffRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await handoffService.CreateAsync(agent.Value.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Ok(new AdminCreatedResponse<HandoffCreated> { Created = result });
        });

        app.MapPut("/admin/agents/{agentName}/handoffs/{handoffId:int}/pickup", async (
            string agentName,
            int handoffId,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var result = await handoffService.PickupAsync(agent.Value.AgentId, handoffId, ct);
            return result is null
                ? Results.NotFound(new ErrorResponse { Error = "Handoff not found or already picked up" })
                : Results.Ok(new AdminPickedUpResponse
                {
                    PickedUp = new AdminPickedUpItem
                    {
                        HandoffId = result.HandoffId,
                        Title = result.Title,
                        PickedUpAt = result.PickedUpAt
                    }
                });
        });

        app.MapDelete("/admin/agents/{agentName}/handoffs/{handoffId:int}", async (
            string agentName,
            int handoffId,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var count = await handoffService.DeleteAsync(agent.Value.AgentId, handoffId, ct);
            return count > 0 ? Results.Ok(new AdminDeletedIdResponse { Deleted = handoffId }) : Results.NotFound();
        });

        // ── Sessions ─────────────────────────────────────────────

        app.MapGet("/admin/agents/{agentName}/sessions/last", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            ISessionService sessionService,
            CancellationToken ct) =>
        {
            var agent = await RequireUserAgent(agentName, ctx, agentService, ct);
            if (agent is null) return Results.NotFound();

            var session = await sessionService.GetLastAsync(agent.Value.AgentId, ct);
            return Results.Ok(new AdminSessionLastResponse
            {
                Agent = agentName,
                LastSession = session is not null ? new AdminLastSession
                {
                    SessionId = session.SessionId,
                    StartedAt = session.StartedAt.ToString("o"),
                    Project = session.Project
                } : null
            });
        });
    }

    /// <summary>
    /// Look up agent by name and verify it belongs to the calling JWT user.
    /// </summary>
    private static async Task<(int AgentId, int UserId)?> RequireUserAgent(
        string agentName, HttpContext ctx, IAgentService agentService, CancellationToken ct)
    {
        var caller = ctx.GetUserContext();
        var agent = await agentService.GetByNameAsync(agentName, ct);
        if (agent is null || agent.UserId != caller.UserId)
            return null;
        return (agent.AgentId, agent.UserId);
    }
}
