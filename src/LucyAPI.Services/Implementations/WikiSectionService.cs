using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class WikiSectionService(WikiSectionRepository repo) : IWikiSectionService
{
    public Task<List<WikiSection>> GetSectionsAsync(int wikiId, CancellationToken ct)
        => repo.GetSectionsAsync(wikiId, ct);

    public Task<List<WikiSection>> GetAsync(int wikiId, int sectionId, CancellationToken ct)
        => repo.GetAsync(wikiId, sectionId, ct);

    public Task<WikiSectionCreated?> CreateAsync(int wikiId, CreateWikiSectionRequest request, CancellationToken ct)
        => repo.CreateAsync(wikiId, request.ParentId, request.Title, request.Description, request.Tags, ct);

    public Task<WikiSectionCreated?> UpdateAsync(int wikiId, int sectionId, UpdateWikiSectionRequest request, CancellationToken ct)
        => repo.UpdateAsync(wikiId, sectionId, request.Title, request.Description, request.Tags, ct);

    public Task<int> DeleteAsync(int wikiId, int sectionId, CancellationToken ct)
        => repo.DeleteAsync(wikiId, sectionId, ct);
}
