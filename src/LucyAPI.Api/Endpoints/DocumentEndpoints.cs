using LucyAPI.Api.Auth;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

/// <summary>
/// Public HTML documents behind the signed, expiring links that get_project / get_wiki return as document_url.
/// No credential: each request is authenticated by the 24h signature from DocumentLinkSigner, so the link
/// works in a phone browser with nothing secret in the URL.
/// </summary>
public static class DocumentEndpoints
{
    private static string LinkExpiredHtml(string kind) =>
        "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
        + "<title>Link expired</title></head><body style=\"font-family:Lexend,system-ui,sans-serif;margin:2rem;\">"
        + "<h1>This link has expired or isn't valid</h1><p>" + kind + " links last 24 hours. Ask for a fresh one.</p></body></html>";

    private static readonly string ProjectLinkExpiredHtml = LinkExpiredHtml("Project");
    private static readonly string WikiLinkExpiredHtml = LinkExpiredHtml("Wiki");

    public static void MapDocumentEndpoints(this WebApplication app)
    {
        app.MapGet("/doc/projects/{projectId:int}", async (
            int projectId,
            int? u,
            long? exp,
            string? sig,
            IProjectService projectService,
            ISectionService sectionService,
            DocumentLinkSigner documentLinks,
            CancellationToken ct) =>
        {
            if (u is null || exp is null || !documentLinks.IsValid(projectId, u.Value, exp.Value, sig))
                return Results.Content(ProjectLinkExpiredHtml, "text/html", statusCode: 403);

            var project = await projectService.GetAsync(projectId, u.Value, ct);
            if (project is null) return Results.NotFound();

            var sections = await sectionService.GetSectionsAsync(u.Value, projectId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderProjectDocument(project, tree);
            return Results.Content(html, "text/html");
        });

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
                return Results.Content(WikiLinkExpiredHtml, "text/html", statusCode: 403);

            var wiki = await wikiService.GetAsync(wikiId, u.Value, ct);
            if (wiki is null) return Results.NotFound();

            var sections = await wikiSectionService.GetSectionsAsync(u.Value, wikiId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderWikiDocument(wiki, tree);
            return Results.Content(html, "text/html");
        });
    }
}
