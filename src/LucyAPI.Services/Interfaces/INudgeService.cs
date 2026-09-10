using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface INudgeService
{
    Task<List<Nudge>> GetAllAsync(int userId, CancellationToken ct = default);
    Task<Nudge?> GetOneAsync(int userId, int nudgeId, CancellationToken ct = default);
    Task<NudgeCreated?> CreateAsync(int userId, CreateNudgeRequest request, CancellationToken ct = default);
    Task<MutationResult?> UpdateAsync(int userId, int nudgeId, UpdateNudgeRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int userId, int nudgeId, CancellationToken ct = default);
    Task<List<ActionableNudge>> GetActionableAsync(int userId, CancellationToken ct = default);
}
