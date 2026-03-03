using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class WikiService(WikiRepository repo) : IWikiService
{
    public Task<List<Wiki>> GetAllAsync(int userId, CancellationToken ct)
        => repo.GetAllAsync(userId, ct);

    public Task<Wiki?> GetAsync(int wikiId, int userId, CancellationToken ct)
        => repo.GetAsync(wikiId, userId, ct);

    public Task<WikiCreated?> CreateAsync(int userId, CreateWikiRequest request, CancellationToken ct)
        => repo.CreateAsync(userId, request.Title, request.Description, ct);

    public Task<WikiCreated?> UpdateAsync(int wikiId, UpdateWikiRequest request, CancellationToken ct)
        => repo.UpdateAsync(wikiId, request.Title, request.Description, ct);

    public Task<int> DeleteAsync(int wikiId, CancellationToken ct)
        => repo.DeleteAsync(wikiId, ct);
}
