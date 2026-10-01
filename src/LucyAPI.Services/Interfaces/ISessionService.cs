namespace LucyAPI.Services.Interfaces;

public interface ISessionService
{
    /// <summary>Attaches a loaded project to the agent's current session. Never throws: tracking must not break the load.</summary>
    Task TryAddProjectAsync(int agentId, int projectId, CancellationToken ct = default);
}
