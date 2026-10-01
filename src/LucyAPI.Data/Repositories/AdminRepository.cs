using Npgsql;

namespace LucyAPI.Data.Repositories;

/// <summary>
/// Admin-specific reads/writes (dashboard, agents list, sharing, secrets list) — all via lucyapi stored functions.
/// </summary>
public sealed class AdminRepository(NpgsqlDataSource dataSource)
{
    // ── Agents with last session ──────────────────────────────────

    public async Task<List<(int AgentId, string Name, int? SessionId, DateTimeOffset? StartedAt, string? Description,
        int[] ProjectIds, string[] ProjectTitles, DateTimeOffset? LastUsedAt)>>
        ListAgentsWithLastSessionAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_admin_agent_list($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string, int?, DateTimeOffset?, string?, int[], string[], DateTimeOffset?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<int[]>(5),
                reader.GetFieldValue<string[]>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7)
            ));
        }
        return results;
    }

    /// <summary>One agent's sessions, newest first, with the projects loaded in each. Caller's own agents only.</summary>
    public async Task<List<(int SessionId, DateTimeOffset StartedAt, string? Description, int[] ProjectIds, string[] ProjectTitles)>>
        ListAgentSessionsAsync(int userId, int agentId, int limit, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_admin_session_list_by_agent($1, $2, $3)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = agentId }, new() { Value = limit } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, DateTimeOffset, string?, int[], string[])>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<int[]>(3),
                reader.GetFieldValue<string[]>(4)
            ));
        }
        return results;
    }

    // ── Sharing ──────────────────────────────────────────────────

    public async Task<List<(int ShareId, int ObjectTypeId, string ObjectType, int ObjectId,
        int? SharedToUserId, string? SharedToName, int? SharedByUserId, string? SharedByName,
        int PermissionLevel, string? ObjectTitle)>>
        ListSharesByMeAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_admin_share_list_by_me($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        return await ReadShareRows(cmd, ct);
    }

    public async Task<List<(int ShareId, int ObjectTypeId, string ObjectType, int ObjectId,
        int? SharedToUserId, string? SharedToName, int? SharedByUserId, string? SharedByName,
        int PermissionLevel, string? ObjectTitle)>>
        ListSharesToMeAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_admin_share_list_to_me($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        return await ReadShareRows(cmd, ct);
    }

    private static async Task<List<(int ShareId, int ObjectTypeId, string ObjectType, int ObjectId,
        int? SharedToUserId, string? SharedToName, int? SharedByUserId, string? SharedByName,
        int PermissionLevel, string? ObjectTitle)>>
        ReadShareRows(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, int, string, int, int?, string?, int?, string?, int, string?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                (int)reader.GetInt16(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                (int)reader.GetInt16(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)
            ));
        }
        return results;
    }

    // ── Secrets (key list with timestamps) ───────────────────────

    public async Task<List<(string Key, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt)>>
        ListSecretKeysAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT key, created_at, updated_at FROM lucyapi.fn_secret_list($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(string, DateTimeOffset?, DateTimeOffset?)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
            ));
        }
        return results;
    }

    // ── Share Update ───────────────────────────────────────────────

    public async Task<(int ShareId, int ObjectTypeId, int ObjectId, int SharedToUserId, int PermissionLevel)?>
        UpdateSharePermissionAsync(int shareId, int userId, int permissionLevel, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_share_update_permission($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = shareId },
                new() { Value = (short)permissionLevel }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4)
        );
    }

    // ── Dashboard CTE ────────────────────────────────────────────

    public async Task<(long Agents, long Projects, long Wikis, long HintCategories,
        long Secrets, long Images, long PendingHandoffs, long SharedToMe)>
        GetDashboardStatsAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_admin_dashboard_stats($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7)
        );
    }

    public async Task<List<(int SessionId, string AgentName, DateTimeOffset? StartedAt, string? Description,
        int[] ProjectIds, string[] ProjectTitles)>>
        GetRecentSessionsAsync(int userId, int limit = 5, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_admin_session_list_recent($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = limit } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string, DateTimeOffset?, string?, int[], string[])>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetFieldValue<int[]>(4),
                reader.GetFieldValue<string[]>(5)
            ));
        }
        return results;
    }
}
