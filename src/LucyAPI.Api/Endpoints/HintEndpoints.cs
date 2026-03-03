using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class HintEndpoints
{
    public static void MapHintEndpoints(this WebApplication app)
    {
        app.MapGet("/hints", async (
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var hints = await hintService.GetAllAsync(caller.UserId, ct);
            var tree = TreeBuilder.Build(hints, h => h.Pkid, h => h.ParentId);
            return Results.Ok(tree);
        });

        app.MapGet("/hints/compact", async (
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var hints = await hintService.GetAllCompactAsync(caller.UserId, ct);
            var tree = TreeBuilder.Build(hints, h => h.Pkid, h => h.ParentId);
            return Results.Ok(tree);
        });

        app.MapGet("/hints/{pkid:int}", async (
            int pkid,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var items = await hintService.GetAsync(pkid, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, h => h.Pkid, h => h.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/hints/categories", async (
            CreateHintCategoryRequest request,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await hintService.CreateCategoryAsync(caller.UserId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/hints/{result.Pkid}", result);
        });

        app.MapPost("/hints", async (
            CreateHintRequest request,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await hintService.CreateAsync(caller.UserId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/hints/{result.Pkid}", result);
        });

        app.MapPut("/hints/{pkid:int}", async (
            int pkid,
            UpdateHintRequest request,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await hintService.UpdateAsync(pkid, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/hints/{pkid:int}", async (
            int pkid,
            HttpContext ctx,
            IHintService hintService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await hintService.DeleteAsync(pkid, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
