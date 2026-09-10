using LucyAPI.Api.Extensions;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class GoogleDocsEndpoints
{
    public static void MapGoogleDocsEndpoints(this WebApplication app)
    {
        // -- Documents --

        app.MapPost("/google/docs", async (
            CreateDocRequest request,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.CreateDocumentAsync(caller.UserId, request.Title, request.Body, ct);
            return Results.Ok(result);
        });

        app.MapGet("/google/docs/{docId}", async (
            string docId,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.ReadDocumentAsync(caller.UserId, docId, ct);
            return Results.Ok(result);
        });

        app.MapPut("/google/docs/{docId}", async (
            string docId,
            UpdateDocRequest request,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.UpdateDocumentAsync(caller.UserId, docId, request.Content, ct);
            return Results.Ok(result);
        });

        app.MapPatch("/google/docs/{docId}/append", async (
            string docId,
            AppendDocRequest request,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.AppendToDocumentAsync(caller.UserId, docId, request.Content, ct);
            return Results.Ok(result);
        });

        // -- Drive file management --

        app.MapGet("/google/drive/files", async (
            string? folderId,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.ListFilesAsync(caller.UserId, folderId, ct);
            return Results.Ok(result);
        });

        app.MapPost("/google/drive/folders", async (
            CreateFolderRequest request,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.CreateFolderAsync(caller.UserId, request.Name, request.ParentFolderId, ct);
            return Results.Ok(result);
        });

        app.MapPut("/google/drive/files/{fileId}/move", async (
            string fileId,
            MoveFileRequest request,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.MoveFileAsync(caller.UserId, fileId, request.TargetFolderId, ct);
            return Results.Ok(result);
        });

        app.MapDelete("/google/drive/files/{fileId}", async (
            string fileId,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.DeleteFileAsync(caller.UserId, fileId, ct);
            return Results.Ok(result);
        });

        app.MapGet("/google/drive/files/{fileId}/meta", async (
            string fileId,
            HttpContext ctx,
            IGoogleDocsService service,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await service.GetFileMetadataAsync(caller.UserId, fileId, ct);
            return Results.Ok(result);
        });
    }
}
