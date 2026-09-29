using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IWikiSectionService
{
    Task<List<WikiSection>> GetSectionsAsync(int userId, int wikiId, CancellationToken ct = default);
    Task<List<WikiSection>> GetAsync(int userId, int wikiId, int sectionId, CancellationToken ct = default);
    Task<WikiSectionCreated?> CreateAsync(int userId, int wikiId, CreateWikiSectionRequest request, CancellationToken ct = default);
    Task<WikiSectionCreated?> UpdateAsync(int userId, int wikiId, int sectionId, UpdateWikiSectionRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int userId, int wikiId, int sectionId, CancellationToken ct = default);
}
