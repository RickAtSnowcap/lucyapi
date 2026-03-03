using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        app.MapGet("/agents/{agentName}/memories", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var memories = await memoryService.GetAllAsync(target.AgentId, ct);
            return Results.Ok(new AgentMemoryListResponse { Agent = agentName, Memories = memories });
        });

        app.MapGet("/agents/{agentName}/memories/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var memory = await memoryService.GetOneAsync(target.AgentId, pkid, ct);
            return memory is null ? Results.NotFound() : Results.Ok(memory);
        });

        app.MapPost("/agents/{agentName}/memories", async (
            string agentName,
            CreateMemoryRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await memoryService.CreateAsync(target.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/agents/{agentName}/memories/{result.Pkid}", result);
        });

        app.MapPut("/agents/{agentName}/memories/{pkid:int}", async (
            string agentName,
            int pkid,
            UpdateMemoryRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await memoryService.UpdateAsync(target.AgentId, pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/agents/{agentName}/memories/{pkid:int}", async (
            string agentName,
            int pkid,
            HttpContext ctx,
            IAgentService agentService,
            IMemoryService memoryService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var count = await memoryService.DeleteAsync(target.AgentId, pkid, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
