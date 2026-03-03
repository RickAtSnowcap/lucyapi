using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class HandoffEndpoints
{
    public static void MapHandoffEndpoints(this WebApplication app)
    {
        app.MapGet("/agents/{agentName}/handoffs", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var handoffs = await handoffService.ListPendingAsync(target.AgentId, ct);
            return Results.Ok(handoffs);
        });

        app.MapGet("/agents/{agentName}/handoffs/{handoffId:int}", async (
            string agentName,
            int handoffId,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var handoff = await handoffService.GetAsync(target.AgentId, handoffId, ct);
            return handoff is null ? Results.NotFound() : Results.Ok(handoff);
        });

        app.MapPost("/agents/{agentName}/handoffs", async (
            string agentName,
            CreateHandoffRequest request,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await handoffService.CreateAsync(target.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/agents/{agentName}/handoffs/{result.HandoffId}", result);
        });

        app.MapPut("/agents/{agentName}/handoffs/{handoffId:int}/pickup", async (
            string agentName,
            int handoffId,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var result = await handoffService.PickupAsync(target.AgentId, handoffId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/agents/{agentName}/handoffs/{handoffId:int}", async (
            string agentName,
            int handoffId,
            HttpContext ctx,
            IAgentService agentService,
            IHandoffService handoffService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var count = await handoffService.DeleteAsync(target.AgentId, handoffId, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
