using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IHandoffService
{
    Task<List<Handoff>> ListPendingAsync(int agentId, CancellationToken ct = default);
    Task<Handoff?> GetAsync(int agentId, int handoffId, CancellationToken ct = default);
    Task<HandoffCreated?> CreateAsync(int agentId, CreateHandoffRequest request, CancellationToken ct = default);
    Task<HandoffPickedUp?> PickupAsync(int agentId, int handoffId, CancellationToken ct = default);
    Task<int> DeleteAsync(int agentId, int handoffId, CancellationToken ct = default);
}
