using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class SessionRepository(NpgsqlDataSource dataSource)
{
    public async Task<SessionCreated?> CreateAsync(int agentId, string? project, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_session_create($1, $2)", conn)
        {
            Parameters =
            {
                new() { Value = agentId },
                new() { Value = (object?)project ?? DBNull.Value }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new SessionCreated
        {
            SessionId = reader.GetInt32(0),
            StartedAt = reader.GetFieldValue<DateTimeOffset>(1)
        };
    }

    public async Task<Session?> GetLastAsync(int agentId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_session_get_last($1)", conn)
        {
            Parameters = { new() { Value = agentId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Session
        {
            SessionId = reader.GetInt32(0),
            StartedAt = reader.GetFieldValue<DateTimeOffset>(1),
            Project = reader.IsDBNull(2) ? null : reader.GetString(2)
        };
    }
}
