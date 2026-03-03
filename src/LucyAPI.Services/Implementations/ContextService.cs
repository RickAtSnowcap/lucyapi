using System.Text.Json;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class ContextService(ContextRepository repo) : IContextService
{
    public Task<JsonDocument?> GetFullAsync(int agentId, int userId, CancellationToken ct)
        => repo.GetFullAsync(agentId, userId, ct);
}
