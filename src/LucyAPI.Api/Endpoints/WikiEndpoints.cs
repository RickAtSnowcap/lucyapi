using LucyAPI.Api.Auth;
using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class WikiEndpoints
{
    private const string DocumentLinkExpiredHtml =
        "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
        + "<title>Link expired</title></head><body style=\"font-family:Lexend,system-ui,sans-serif;margin:2rem;\">"
        + "<h1>This link has expired or isn't valid</h1><p>Wiki links last 24 hours. Ask for a fresh one.</p></body></html>";

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
            DocumentLinkSigner documentLinks,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var wiki = await wikiService.GetAsync(wikiId, caller.UserId, ct);
            if (wiki is null) return Results.NotFound();

            wiki.DocumentUrl = documentLinks.CreateWikiUrl(wikiId, caller.UserId);

            var sections = await wikiSectionService.GetSectionsAsync(caller.UserId, wikiId, ct);
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

            var sections = await wikiSectionService.GetSectionsAsync(caller.UserId, wikiId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderWikiDocument(wiki, tree);
            return Results.Content(html, "text/html");
        });

        // Public (see ApiKeyAuthMiddleware): authenticated by the 24h signature from DocumentLinkSigner,
        // so the link works in a phone browser with no credential in the URL.
        app.MapGet("/doc/wikis/{wikiId:int}", async (
            int wikiId,
            int? u,
            long? exp,
            string? sig,
            IWikiService wikiService,
            IWikiSectionService wikiSectionService,
            DocumentLinkSigner documentLinks,
            CancellationToken ct) =>
        {
            if (u is null || exp is null || !documentLinks.IsValidWiki(wikiId, u.Value, exp.Value, sig))
                return Results.Content(DocumentLinkExpiredHtml, "text/html", statusCode: 403);

            var wiki = await wikiService.GetAsync(wikiId, u.Value, ct);
            if (wiki is null) return Results.NotFound();

            var sections = await wikiSectionService.GetSectionsAsync(u.Value, wikiId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderWikiDocument(wiki, tree);
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
            var result = await wikiService.UpdateAsync(caller.UserId, wikiId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/wikis/{wikiId:int}", async (
            int wikiId,
            HttpContext ctx,
            IWikiService wikiService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await wikiService.DeleteAsync(caller.UserId, wikiId, ct);
            return count is int deleted ? Results.Ok(new SectionsDeletedResponse { SectionsDeleted = deleted }) : Results.NotFound();
        });
    }
}
