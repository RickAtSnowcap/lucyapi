using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IHandoffService
{
    Task<List<Handoff>> ListPendingAsync(int agentId, CancellationToken ct = default);
    /// <summary>A handoff the caller received or created.</summary>
    Task<Handoff?> GetAsync(int callerAgentId, int handoffId, CancellationToken ct = default);
    /// <param name="agentId">Recipient.</param>
    /// <param name="createdByAgentId">Sending agent; null for an admin (not an agent).</param>
    Task<HandoffCreated?> CreateAsync(int agentId, CreateHandoffRequest request, int? createdByAgentId, CancellationToken ct = default);
    Task<HandoffPickedUp?> PickupAsync(int agentId, int handoffId, CancellationToken ct = default);
    /// <summary>Recipient any time; creator only while pending.</summary>
    Task<HandoffChange> DeleteAsync(int callerAgentId, int handoffId, CancellationToken ct = default);
    /// <summary>Creator only, while pending; null title/prompt = keep.</summary>
    Task<HandoffChange> UpdateAsync(int callerAgentId, int handoffId, string? title, string? prompt, CancellationToken ct = default);
    Task<List<HandoffSent>> ListSentAsync(int callerAgentId, bool pendingOnly, CancellationToken ct = default);
}
