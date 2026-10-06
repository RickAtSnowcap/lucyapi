using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class AgentService(AgentRepository repo) : IAgentService
{
    public Task<AgentRef?> GetByNameAsync(string agentName, CancellationToken ct)
        => repo.GetByNameAsync(agentName, ct);

}
