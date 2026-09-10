using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class AgentRepository(NpgsqlDataSource dataSource)
{
    public async Task<Agent?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_agent_get_by_api_key($1)", conn)
        {
            Parameters = { new() { Value = apiKey } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Agent
        {
            AgentId = reader.GetInt32(0),
            AgentName = reader.GetString(1),
            UserId = reader.GetInt32(2),
            UserName = reader.GetString(3)
        };
    }

    public async Task<string?> GetFirstKeyByUserIdAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT lucyapi.fn_agent_get_first_key_by_user_id($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

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
