using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class NudgeService(NudgeRepository repo) : INudgeService
{
    public Task<List<Nudge>> GetAllAsync(int userId, CancellationToken ct = default)
        => repo.GetAllAsync(userId, ct);

    public Task<Nudge?> GetOneAsync(int userId, int nudgeId, CancellationToken ct = default)
        => repo.GetOneAsync(userId, nudgeId, ct);

    public Task<NudgeCreated?> CreateAsync(int userId, CreateNudgeRequest request, CancellationToken ct = default)
        => repo.CreateAsync(userId, request.Title, request.Description, request.DueDate, ct);

    public async Task<MutationResult?> UpdateAsync(int userId, int nudgeId, UpdateNudgeRequest request, CancellationToken ct = default)
    {
        DateOnly? dueDate;
        if (request.ClearDueDate)
        {
            // Caller wants ASAP — pass null to overwrite due_date
            dueDate = null;
        }
        else if (request.DueDate.HasValue)
        {
            // Caller provided a new date — use it
            dueDate = request.DueDate;
        }
        else
        {
            // Caller didn't touch due_date — fetch current value to preserve it
            var current = await repo.GetOneAsync(userId, nudgeId, ct);
            if (current is null) return null;
            dueDate = current.DueDate;
        }

        return await repo.UpdateAsync(userId, nudgeId, request.Title, request.Description, dueDate, ct);
    }

    public Task<int> DeleteAsync(int userId, int nudgeId, CancellationToken ct = default)
        => repo.DeleteAsync(userId, nudgeId, ct);

    public Task<List<ActionableNudge>> GetActionableAsync(int userId, CancellationToken ct = default)
        => repo.GetActionableAsync(userId, ct);
}
