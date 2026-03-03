using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IPreferenceService
{
    Task<List<PreferenceTopLevel>> GetTopLevelAsync(int agentId, CancellationToken ct = default);
    Task<List<Preference>> GetBranchAsync(int agentId, int pkid, CancellationToken ct = default);
    Task<MutationResult?> CreateAsync(int agentId, CreatePreferenceRequest request, CancellationToken ct = default);
    Task<MutationResult?> UpdateAsync(int agentId, int pkid, UpdatePreferenceRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct = default);
}
