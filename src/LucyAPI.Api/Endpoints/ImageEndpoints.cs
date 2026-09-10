using LucyAPI.Api.Extensions;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class ImageEndpoints
{
    public static void MapImageEndpoints(this WebApplication app)
    {
        app.MapPost("/genimage", async (
            GenImageRequest request,
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.GenerateAsync(caller.UserId, request, ct);
            return Results.Ok(result);
        });

        app.MapPost("/genimage/edit", async (
            EditImageRequest request,
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.EditAsync(caller.UserId, request, ct);
            return Results.Ok(result);
        });

        app.MapPost("/genimage/analyze", async (
            AnalyzeImageRequest request,
            IImageService service,
            CancellationToken ct) =>
        {
            var result = await service.AnalyzeAsync(request, ct);
            return Results.Ok(result);
        });

        // Cleanup before /{imageId} routes to avoid route collision
        app.MapPost("/images/cleanup", async (
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.CleanupAsync(caller.UserId, ct);
            return Results.Ok(result);
        });

        app.MapGet("/images", async (
            bool? keep,
            int? limit,
            int? offset,
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.ListAsync(caller.UserId, keep, limit ?? 50, offset ?? 0, ct);
            return Results.Ok(result);
        });

        app.MapGet("/images/{imageId:int}", async (
            int imageId,
            IImageService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAsync(imageId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapPatch("/images/{imageId:int}", async (
            int imageId,
            KeepImageRequest request,
            IImageService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateKeepAsync(imageId, request.Keep, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/images/{imageId:int}", async (
            int imageId,
            bool? force,
            IImageService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(imageId, force ?? false, ct);
            if (result.Detail == "Not found") return Results.NotFound();
            if (!result.Deleted) return Results.Conflict(result);
            return Results.Ok(result);
        });
    }
}
