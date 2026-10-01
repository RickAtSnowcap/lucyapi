using Npgsql;

namespace LucyAPI.Data.Repositories;

/// <summary>
/// Sessions are opened by get_context (fn_context_get_full). This records the projects an agent loads
/// during its current session.
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
}
