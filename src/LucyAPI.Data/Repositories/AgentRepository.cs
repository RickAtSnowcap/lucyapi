using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class AgentRepository(NpgsqlDataSource dataSource)
{
    public async Task<AgentRef?> GetByNameAsync(string agentName, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_agent_get_by_name($1)", conn)
        {
            Parameters = { new() { Value = agentName } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AgentRef
        {
            AgentId = reader.GetInt32(0),
            UserId = reader.GetInt32(1)
        };
    }
}
