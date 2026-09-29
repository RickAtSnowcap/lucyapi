using LucyAPI.Data.Models;

namespace LucyAPI.Services.Interfaces;

public interface IAgentService
{
    Task<Agent?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);
    Task<AgentRef?> GetByNameAsync(string agentName, CancellationToken ct = default);
}
