using LucyAPI.Api.Models;
using LucyAPI.Data.Models;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Extensions;

public static class AgentResolutionExtensions
{
    /// <summary>
    /// Resolves an agent named in a REST route, but only if it belongs to the caller's user — the same rule
    /// MCP applies (ResolveAgentId). Another user's agent is reported as not found, so names don't leak.
    /// </summary>
    public static async Task<AgentRef?> GetSameUserAgentAsync(
        this IAgentService agentService, AgentContext caller, string agentName, CancellationToken ct)
    {
        var target = await agentService.GetByNameAsync(agentName, ct);
        return target is not null && target.UserId == caller.UserId ? target : null;
    }
}
