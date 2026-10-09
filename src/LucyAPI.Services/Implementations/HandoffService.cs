using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class HandoffService(HandoffRepository repo) : IHandoffService
{
    public Task<List<Handoff>> ListPendingAsync(int agentId, CancellationToken ct)
        => repo.ListPendingAsync(agentId, ct);

    public Task<Handoff?> GetAsync(int callerAgentId, int handoffId, CancellationToken ct)
        => repo.GetAsync(callerAgentId, handoffId, ct);

    public Task<HandoffCreated?> CreateAsync(int agentId, CreateHandoffRequest request, int? createdByAgentId, CancellationToken ct)
        => repo.CreateAsync(agentId, request.Title, request.Prompt, createdByAgentId, ct);

    public Task<HandoffPickedUp?> PickupAsync(int agentId, int handoffId, CancellationToken ct)
        => repo.PickupAsync(agentId, handoffId, ct);

    public Task<HandoffChange> DeleteAsync(int callerAgentId, int handoffId, CancellationToken ct)
        => repo.DeleteAsync(callerAgentId, handoffId, ct);

    public Task<HandoffChange> UpdateAsync(int callerAgentId, int handoffId, string? title, string? prompt, CancellationToken ct)
        => repo.UpdateAsync(callerAgentId, handoffId, title, prompt, ct);

    public Task<List<HandoffSent>> ListSentAsync(int callerAgentId, bool pendingOnly, CancellationToken ct)
        => repo.ListSentAsync(callerAgentId, pendingOnly, ct);
}
