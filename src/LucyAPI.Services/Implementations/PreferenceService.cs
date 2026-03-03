using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class PreferenceService(PreferenceRepository repo) : IPreferenceService
{
    public Task<List<PreferenceTopLevel>> GetTopLevelAsync(int agentId, CancellationToken ct)
        => repo.GetTopLevelAsync(agentId, ct);

    public Task<List<Preference>> GetBranchAsync(int agentId, int pkid, CancellationToken ct)
        => repo.GetBranchAsync(agentId, pkid, ct);

    public Task<MutationResult?> CreateAsync(int agentId, CreatePreferenceRequest request, CancellationToken ct)
        => repo.CreateAsync(agentId, request.ParentId, request.Title, request.Description, ct);

    public Task<MutationResult?> UpdateAsync(int agentId, int pkid, UpdatePreferenceRequest request, CancellationToken ct)
        => repo.UpdateAsync(agentId, pkid, request.Title, request.Description, ct);

    public Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct)
        => repo.DeleteAsync(agentId, pkid, ct);
}
