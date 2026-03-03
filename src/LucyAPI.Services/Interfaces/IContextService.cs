using System.Text.Json;

namespace LucyAPI.Services.Interfaces;

public interface IContextService
{
    Task<JsonDocument?> GetFullAsync(int agentId, int userId, CancellationToken ct = default);
}
