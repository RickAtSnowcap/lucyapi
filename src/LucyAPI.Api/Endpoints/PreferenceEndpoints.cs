using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class PreferenceEndpoints
{
    public static void MapPreferenceEndpoints(this WebApplication app)
    {
        app.MapGet("/agents/{agentName}/preferences", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var preferences = await preferenceService.GetTopLevelAsync(target.AgentId, ct);
            return Results.Ok(new AgentPreferenceListResponse { Agent = agentName, Preferences = preferences });
        });

        app.MapGet("/agents/{agentName}/preferences/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var items = await preferenceService.GetBranchAsync(target.AgentId, pkid, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = LucyAPI.Services.Utilities.TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/agents/{agentName}/preferences", async (
            string agentName,
            CreatePreferenceRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await preferenceService.CreateAsync(target.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/agents/{agentName}/preferences/{result.Id}", result);
        });

        app.MapPut("/agents/{agentName}/preferences/{pkid:int}", async (
            string agentName,
            int pkid,
            UpdatePreferenceRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await preferenceService.UpdateAsync(target.AgentId, pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/agents/{agentName}/preferences/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IPreferenceService preferenceService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var count = await preferenceService.DeleteAsync(target.AgentId, pkid, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
