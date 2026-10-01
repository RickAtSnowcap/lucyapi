using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
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

    public const int MaxDescriptionLength = 500;

    public async Task<SessionDescriptionResponse?> SetDescriptionAsync(int agentId, string description, CancellationToken ct)
    {
        var text = description.Trim();
        if (text.Length == 0)
            throw new InvalidOperationException("description is required");
        if (text.Length > MaxDescriptionLength)
            throw new InvalidOperationException($"description is longer than {MaxDescriptionLength} characters");

        var row = await repo.SetDescriptionAsync(agentId, text, ct);
        return row is { } r
            ? new SessionDescriptionResponse { SessionId = r.SessionId, StartedAt = r.StartedAt.ToString("o"), Description = r.Description }
            : null;
    }
}
