using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IAlwaysLoadService
{
    Task<List<AlwaysLoadItem>> GetAllAsync(int agentId, CancellationToken ct = default);
    Task<List<AlwaysLoadItem>> GetItemAsync(int agentId, int pkid, CancellationToken ct = default);
    Task<AlwaysLoadCreated?> CreateAsync(int agentId, CreateAlwaysLoadRequest request, CancellationToken ct = default);
    Task<MutationResult?> UpdateAsync(int agentId, int pkid, UpdateAlwaysLoadRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct = default);
}
