using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class SectionEndpoints
{
    public static void MapSectionEndpoints(this WebApplication app)
    {
        app.MapGet("/projects/{projectId:int}/sections/{sectionId:int}", async (
            int projectId,
            int sectionId,
            HttpContext ctx,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var items = await sectionService.GetAsync(projectId, sectionId, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, s => s.SectionId, s => s.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/projects/{projectId:int}/sections", async (
            int projectId,
            CreateSectionRequest request,
            HttpContext ctx,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await sectionService.CreateAsync(projectId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/projects/{projectId}/sections/{result.SectionId}", result);
        });

        app.MapPut("/projects/{projectId:int}/sections/{sectionId:int}", async (
            int projectId,
            int sectionId,
            UpdateSectionRequest request,
            HttpContext ctx,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await sectionService.UpdateAsync(projectId, sectionId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/projects/{projectId:int}/sections/{sectionId:int}", async (
            int projectId,
            int sectionId,
            HttpContext ctx,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await sectionService.DeleteAsync(projectId, sectionId, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
