using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class WikiTagService(WikiTagRepository repo) : IWikiTagService
{
    public Task<List<string>> GetTagsAsync(int userId, int wikiId, CancellationToken ct)
        => repo.GetTagsAsync(userId, wikiId, ct);

    public Task<List<WikiTagSearchResult>> SearchAsync(string tag, int userId, CancellationToken ct)
        => repo.SearchAsync(tag, userId, ct);
}
