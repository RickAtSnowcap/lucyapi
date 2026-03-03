using System.Text.Json;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class ContextRepository(NpgsqlDataSource dataSource)
{
    public async Task<JsonDocument?> GetFullAsync(int agentId, int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_context_get_full($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var json = reader.GetString(0);
        return JsonDocument.Parse(json);
    }
}
