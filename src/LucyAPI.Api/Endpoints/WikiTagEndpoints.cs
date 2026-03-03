using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class WikiTagEndpoints
{
    public static void MapWikiTagEndpoints(this WebApplication app)
    {
        app.MapGet("/wikis/{wikiId:int}/tags", async (
            int wikiId,
            HttpContext ctx,
            IWikiTagService wikiTagService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var tags = await wikiTagService.GetTagsAsync(wikiId, ct);
            return Results.Ok(new WikiTagListResponse { WikiId = wikiId, Tags = tags });
        });

        app.MapGet("/wikis/tags/{tag}", async (
            string tag,
            HttpContext ctx,
            IWikiTagService wikiTagService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var results = await wikiTagService.SearchAsync(tag, caller.UserId, ct);
            return Results.Ok(new TagSearchResponse { Tag = tag, Results = results });
        });
    }
}
