using LucyAPI.Data.Models;

namespace LucyAPI.Services.Interfaces;

public interface IAgentService
{
    Task<AgentRef?> GetByNameAsync(string agentName, CancellationToken ct = default);
}
