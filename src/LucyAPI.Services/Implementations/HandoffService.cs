using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class HandoffService(HandoffRepository repo) : IHandoffService
{
    public Task<List<Handoff>> ListPendingAsync(int agentId, CancellationToken ct)
        => repo.ListPendingAsync(agentId, ct);

    public Task<Handoff?> GetAsync(int agentId, int handoffId, CancellationToken ct)
        => repo.GetAsync(agentId, handoffId, ct);

    public Task<HandoffCreated?> CreateAsync(int agentId, CreateHandoffRequest request, CancellationToken ct)
        => repo.CreateAsync(agentId, request.Title, request.Prompt, ct);

    public Task<HandoffPickedUp?> PickupAsync(int agentId, int handoffId, CancellationToken ct)
        => repo.PickupAsync(agentId, handoffId, ct);

    public Task<int> DeleteAsync(int agentId, int handoffId, CancellationToken ct)
        => repo.DeleteAsync(agentId, handoffId, ct);
}
