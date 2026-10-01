using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface ISessionService
{
    /// <summary>Attaches a loaded project to the agent's current session. Never throws: tracking must not break the load.</summary>
    Task TryAddProjectAsync(int agentId, int projectId, CancellationToken ct = default);

    /// <summary>Sets the description of the agent's current session. Null if the agent has no session yet.</summary>
    Task<SessionDescriptionResponse?> SetDescriptionAsync(int agentId, string description, CancellationToken ct = default);
}
