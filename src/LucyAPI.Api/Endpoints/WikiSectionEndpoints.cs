using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class WikiSectionEndpoints
{
    public static void MapWikiSectionEndpoints(this WebApplication app)
    {
        app.MapGet("/wikis/{wikiId:int}/sections/{sectionId:int}", async (
            int wikiId,
            int sectionId,
            HttpContext ctx,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var items = await wikiSectionService.GetAsync(wikiId, sectionId, ct);
            if (items.Count == 0) return Results.NotFound();

            var tree = TreeBuilder.Build(items, s => s.SectionId, s => s.ParentId, items[0].ParentId);
            return Results.Ok(tree.Count > 0 ? tree[0] : null);
        });

        app.MapPost("/wikis/{wikiId:int}/sections", async (
            int wikiId,
            CreateWikiSectionRequest request,
            HttpContext ctx,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await wikiSectionService.CreateAsync(wikiId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/wikis/{wikiId}/sections/{result.SectionId}", result);
        });

        app.MapPut("/wikis/{wikiId:int}/sections/{sectionId:int}", async (
            int wikiId,
            int sectionId,
            UpdateWikiSectionRequest request,
            HttpContext ctx,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await wikiSectionService.UpdateAsync(wikiId, sectionId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/wikis/{wikiId:int}/sections/{sectionId:int}", async (
            int wikiId,
            int sectionId,
            HttpContext ctx,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await wikiSectionService.DeleteAsync(wikiId, sectionId, ct);
            return count > 0 ? Results.Ok(new DeleteCountResponse { DeletedCount = count }) : Results.NotFound();
        });
    }
}
