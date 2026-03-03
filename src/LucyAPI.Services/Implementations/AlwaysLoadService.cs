using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class AlwaysLoadService(AlwaysLoadRepository repo) : IAlwaysLoadService
{
    public Task<List<AlwaysLoadItem>> GetAllAsync(int agentId, CancellationToken ct)
        => repo.GetAllAsync(agentId, ct);

    public Task<List<AlwaysLoadItem>> GetItemAsync(int agentId, int pkid, CancellationToken ct)
        => repo.GetItemAsync(agentId, pkid, ct);

    public Task<AlwaysLoadCreated?> CreateAsync(int agentId, CreateAlwaysLoadRequest request, CancellationToken ct)
        => repo.CreateAsync(agentId, request.ParentId, request.Title, request.Description, ct);

    public Task<MutationResult?> UpdateAsync(int agentId, int pkid, UpdateAlwaysLoadRequest request, CancellationToken ct)
        => repo.UpdateAsync(agentId, pkid, request.Title, request.Description, ct);

    public Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct)
        => repo.DeleteAsync(agentId, pkid, ct);
}
