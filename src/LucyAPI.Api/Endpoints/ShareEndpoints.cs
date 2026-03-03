using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class ShareEndpoints
{
    public static void MapShareEndpoints(this WebApplication app)
    {
        app.MapPost("/sharing", async (
            ShareObjectRequest request,
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await shareService.ShareAsync(caller.UserId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/sharing/{result.ShareId}", result);
        });

        app.MapDelete("/sharing/{shareId:int}", async (
            int shareId,
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var success = await shareService.RevokeAsync(caller.UserId, shareId, ct);
            return success ? Results.Ok(new StatusResponse { Status = "revoked" }) : Results.NotFound();
        });

        app.MapGet("/sharing/by-me", async (
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var shares = await shareService.GetSharedByMeAsync(caller.UserId, ct);
            return Results.Ok(shares);
        });

        app.MapGet("/sharing/to-me", async (
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var shares = await shareService.GetSharedToMeAsync(caller.UserId, ct);
            return Results.Ok(shares);
        });

        app.MapGet("/sharing/check", async (
            int objectTypeId,
            int objectId,
            HttpContext ctx,
            IShareService shareService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await shareService.CheckPermissionAsync(caller.UserId, objectTypeId, objectId, ct);
            return Results.Ok(result);
        });
    }
}
