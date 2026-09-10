using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class NudgeEndpoints
{
    public static void MapNudgeEndpoints(this WebApplication app)
    {
        app.MapGet("/nudges", async (
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var nudges = await nudgeService.GetAllAsync(caller.UserId, ct);
            return Results.Ok(new NudgeListResponse { Nudges = nudges });
        });

        app.MapGet("/nudges/{nudgeId:int}", async (
            int nudgeId,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var nudge = await nudgeService.GetOneAsync(caller.UserId, nudgeId, ct);
            return nudge is null ? Results.NotFound() : Results.Ok(nudge);
        });

        app.MapPost("/nudges", async (
            CreateNudgeRequest request,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await nudgeService.CreateAsync(caller.UserId, request, ct);
            return result is null
                ? Results.BadRequest()
                : Results.Created($"/nudges/{result.NudgeId}", result);
        });

        app.MapPut("/nudges/{nudgeId:int}", async (
            int nudgeId,
            UpdateNudgeRequest request,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await nudgeService.UpdateAsync(caller.UserId, nudgeId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/nudges/{nudgeId:int}", async (
            int nudgeId,
            HttpContext ctx,
            INudgeService nudgeService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await nudgeService.DeleteAsync(caller.UserId, nudgeId, ct);
            return count == 0 ? Results.NotFound() : Results.Ok(new DeleteCountResponse { DeletedCount = count });
        });
    }
}
