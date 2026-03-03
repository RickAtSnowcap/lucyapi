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

    public Task<List<Hint>> GetAsync(int pkid, CancellationToken ct)
        => repo.GetAsync(pkid, ct);

    public Task<HintCreated?> CreateCategoryAsync(int userId, CreateHintCategoryRequest request, CancellationToken ct)
        => repo.CreateCategoryAsync(userId, request.Title, request.Description, ct);

    public Task<HintCreated?> CreateAsync(int userId, CreateHintRequest request, CancellationToken ct)
        => repo.CreateAsync(userId, request.ParentId, request.Title, request.Description, ct);

    public Task<MutationResult?> UpdateAsync(int pkid, UpdateHintRequest request, CancellationToken ct)
        => repo.UpdateAsync(pkid, request.Title, request.Description, ct);

    public Task<int> DeleteAsync(int pkid, CancellationToken ct)
        => repo.DeleteAsync(pkid, ct);
}
