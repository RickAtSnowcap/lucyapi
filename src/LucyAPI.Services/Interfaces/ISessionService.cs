using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface ISessionService
{
    Task<SessionCreated?> CreateAsync(int agentId, CreateSessionRequest request, CancellationToken ct = default);
    Task<Session?> GetLastAsync(int agentId, CancellationToken ct = default);
}
