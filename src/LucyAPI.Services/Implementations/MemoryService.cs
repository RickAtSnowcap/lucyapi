using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class MemoryService(MemoryRepository repo) : IMemoryService
{
    public Task<List<Memory>> GetAllAsync(int agentId, CancellationToken ct)
        => repo.GetAllAsync(agentId, ct);

    public Task<Memory?> GetOneAsync(int agentId, int pkid, CancellationToken ct)
        => repo.GetOneAsync(agentId, pkid, ct);

    public Task<MemoryCreated?> CreateAsync(int agentId, CreateMemoryRequest request, CancellationToken ct)
        => repo.CreateAsync(agentId, request.Title, request.Description, ct);

    public Task<MutationResult?> UpdateAsync(int agentId, int pkid, UpdateMemoryRequest request, CancellationToken ct)
        => repo.UpdateAsync(agentId, pkid, request.Title, request.Description, ct);

    public Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct)
        => repo.DeleteAsync(agentId, pkid, ct);
}
