using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class AlwaysLoadEndpoints
{
    public static void MapAlwaysLoadEndpoints(this WebApplication app)
    {
        app.MapGet("/agents/{agentName}/context/always_load", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var items = await alwaysLoadService.GetAllAsync(target.AgentId, ct);
            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId);
            return Results.Ok(new AgentAlwaysLoadListResponse { Agent = agentName, AlwaysLoad = tree });
        });

        app.MapGet("/agents/{agentName}/context/always_load/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var items = await alwaysLoadService.GetItemAsync(target.AgentId, pkid, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/agents/{agentName}/context/always_load", async (
            string agentName,
            CreateAlwaysLoadRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await alwaysLoadService.CreateAsync(target.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/agents/{agentName}/context/always_load/{result.Pkid}", result);
        });

        app.MapPut("/agents/{agentName}/context/always_load/{pkid:int}", async (
            string agentName,
            int pkid,
            UpdateAlwaysLoadRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await alwaysLoadService.UpdateAsync(target.AgentId, pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/agents/{agentName}/context/always_load/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IAlwaysLoadService alwaysLoadService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var count = await alwaysLoadService.DeleteAsync(target.AgentId, pkid, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
