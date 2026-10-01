using LucyAPI.Data.Repositories;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class SessionService(SessionRepository repo) : ISessionService
{
    public async Task TryAddProjectAsync(int agentId, int projectId, CancellationToken ct)
    {
        try
        {
            await repo.AddProjectAsync(agentId, projectId, ct);
        }
        catch (Exception ex)
        {
            try { Console.Error.WriteLine($"session: add project {projectId} for agent {agentId} failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* logging must never throw */ }
        }
    }
}
