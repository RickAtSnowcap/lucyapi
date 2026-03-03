using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class WikiEndpoints
{
    public static void MapWikiEndpoints(this WebApplication app)
    {
        app.MapGet("/wikis", async (
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var wikis = await wikiService.GetAllAsync(caller.UserId, ct);
            return Results.Ok(wikis);
        });

        app.MapGet("/wikis/{wikiId:int}", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var sections = await wikiSectionService.GetSectionsAsync(wikiId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            return Results.Ok(new WikiDetailResponse { Wiki = wiki, Sections = tree });
        });

        app.MapGet("/wikis/{wikiId:int}/document", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            var sections = await wikiSectionService.GetSectionsAsync(wikiId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderWikiDocument(tree);
            return Results.Content(html, "text/html");
        });

        app.MapPost("/wikis", async (
            CreateWikiRequest request,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await wikiService.CreateAsync(caller.UserId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/wikis/{result.WikiId}", result);
        });

        app.MapPut("/wikis/{wikiId:int}", async (
            int wikiId,
            UpdateWikiRequest request,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await wikiService.UpdateAsync(wikiId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/wikis/{wikiId:int}", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await wikiService.DeleteAsync(wikiId, ct);
            return count >= 0 ? Results.Ok(new SectionsDeletedResponse { SectionsDeleted = count }) : Results.NotFound();
        });
    }
}
