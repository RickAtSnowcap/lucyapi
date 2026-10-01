using Npgsql;

namespace LucyAPI.Data.Repositories;

/// <summary>
/// Sessions are opened by get_context (fn_context_get_full). This records the projects an agent loads
/// during its current session, and the description the agent gives it.
/// </summary>
public sealed class SessionRepository(NpgsqlDataSource dataSource)
{
    public async Task AddProjectAsync(int agentId, int projectId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT lucyapi.fn_session_add_project($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = projectId } }
        };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Overwrites the description of the agent's current session. Null if the agent has no session yet.</summary>
    public async Task<(int SessionId, DateTimeOffset StartedAt, string? Description)?> SetDescriptionAsync(
        int agentId, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_session_set_description($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = description } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }
}
