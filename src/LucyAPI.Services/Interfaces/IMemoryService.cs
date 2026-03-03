using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IMemoryService
{
    Task<List<Memory>> GetAllAsync(int agentId, CancellationToken ct = default);
    Task<Memory?> GetOneAsync(int agentId, int pkid, CancellationToken ct = default);
    Task<MemoryCreated?> CreateAsync(int agentId, CreateMemoryRequest request, CancellationToken ct = default);
    Task<MutationResult?> UpdateAsync(int agentId, int pkid, UpdateMemoryRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct = default);
}
