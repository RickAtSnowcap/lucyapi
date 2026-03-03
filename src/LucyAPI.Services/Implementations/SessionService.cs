using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class SessionService(SessionRepository repo) : ISessionService
{
    public Task<SessionCreated?> CreateAsync(int agentId, CreateSessionRequest request, CancellationToken ct)
        => repo.CreateAsync(agentId, request.Project, ct);

    public Task<Session?> GetLastAsync(int agentId, CancellationToken ct)
        => repo.GetLastAsync(agentId, ct);
}
