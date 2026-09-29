using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class HintService(HintRepository repo) : IHintService
{
    public Task<List<Hint>> GetAllAsync(int userId, CancellationToken ct)
        => repo.GetAllAsync(userId, ct);

    public Task<List<HintCompact>> GetAllCompactAsync(int userId, CancellationToken ct)
        => repo.GetAllCompactAsync(userId, ct);

    public Task<List<Hint>> GetAsync(int userId, int pkid, CancellationToken ct)
        => repo.GetAsync(userId, pkid, ct);

    public Task<HintCreated?> CreateCategoryAsync(int userId, CreateHintCategoryRequest request, CancellationToken ct)
        => repo.CreateCategoryAsync(userId, request.ParentId, request.Title, request.Description, request.SortOrder, ct);

    public Task<HintCreated?> CreateAsync(int userId, CreateHintRequest request, CancellationToken ct)
        => repo.CreateAsync(userId, request.ParentId, request.Title, request.Description, request.SortOrder, ct);

    public Task<MutationResult?> UpdateAsync(int userId, int pkid, UpdateHintRequest request, CancellationToken ct)
        => repo.UpdateAsync(userId, pkid, request.Title, request.Description, request.SortOrder, ct);

    public Task<int> DeleteAsync(int userId, int pkid, CancellationToken ct)
        => repo.DeleteAsync(userId, pkid, ct);

    public Task<int> DeleteCategoryAsync(int userId, int pkid, CancellationToken ct)
        => repo.DeleteCategoryAsync(userId, pkid, ct);
}
