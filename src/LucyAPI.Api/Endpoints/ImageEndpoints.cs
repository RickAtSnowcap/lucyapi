using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
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
            try
            {
                var result = await service.EditAsync(caller.UserId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ErrorResponse { Error = ex.Message });
            }
        });

        app.MapPost("/genimage/analyze", async (
            AnalyzeImageRequest request,
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            try
            {
                var result = await service.AnalyzeAsync(caller.UserId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new ErrorResponse { Error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ErrorResponse { Error = ex.Message });
            }
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
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.GetAsync(caller.UserId, imageId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapPatch("/images/{imageId:int}", async (
            int imageId,
            KeepImageRequest request,
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.UpdateKeepAsync(caller.UserId, imageId, request.Keep, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/images/{imageId:int}", async (
            int imageId,
            bool? force,
            HttpContext ctx,
            IImageService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.DeleteAsync(caller.UserId, imageId, force ?? false, ct);
            if (result.Detail == "Not found") return Results.NotFound();
            if (!result.Deleted) return Results.Conflict(result);
            return Results.Ok(result);
        });
    }
}
