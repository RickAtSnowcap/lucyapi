using LucyAPI.Data.Models;

namespace LucyAPI.Services.Interfaces;

public interface IWikiTagService
{
    Task<List<string>> GetTagsAsync(int userId, int wikiId, CancellationToken ct = default);
    Task<List<WikiTagSearchResult>> SearchAsync(string tag, int userId, CancellationToken ct = default);
}
