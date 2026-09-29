using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class WikiSectionService(WikiSectionRepository repo) : IWikiSectionService
{
    public Task<List<WikiSection>> GetSectionsAsync(int userId, int wikiId, CancellationToken ct)
        => repo.GetSectionsAsync(userId, wikiId, ct);

    public Task<List<WikiSection>> GetAsync(int userId, int wikiId, int sectionId, CancellationToken ct)
        => repo.GetAsync(userId, wikiId, sectionId, ct);

    public Task<WikiSectionCreated?> CreateAsync(int userId, int wikiId, CreateWikiSectionRequest request, CancellationToken ct)
        => repo.CreateAsync(userId, wikiId, request.ParentId, request.Title, request.Description, request.Tags, ct);

    public Task<WikiSectionCreated?> UpdateAsync(int userId, int wikiId, int sectionId, UpdateWikiSectionRequest request, CancellationToken ct)
        => repo.UpdateAsync(userId, wikiId, sectionId, request.Title, request.Description, request.Tags, ct);

    public Task<int> DeleteAsync(int userId, int wikiId, int sectionId, CancellationToken ct)
        => repo.DeleteAsync(userId, wikiId, sectionId, ct);
}
