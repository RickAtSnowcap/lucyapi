using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IWikiSectionService
{
    Task<List<WikiSection>> GetSectionsAsync(int wikiId, CancellationToken ct = default);
    Task<List<WikiSection>> GetAsync(int wikiId, int sectionId, CancellationToken ct = default);
    Task<WikiSectionCreated?> CreateAsync(int wikiId, CreateWikiSectionRequest request, CancellationToken ct = default);
    Task<WikiSectionCreated?> UpdateAsync(int wikiId, int sectionId, UpdateWikiSectionRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int wikiId, int sectionId, CancellationToken ct = default);
}
