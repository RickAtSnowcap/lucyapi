using LucyAPI.Data.Models;
using Npgsql;
using NpgsqlTypes;

namespace LucyAPI.Data.Repositories;

public sealed class NudgeRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<Nudge>> GetAllAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_nudge_get_all($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Nudge>();
        while (await reader.ReadAsync(ct))
            results.Add(MapNudge(reader));
        return results;
    }

    public async Task<Nudge?> GetOneAsync(int userId, int nudgeId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_nudge_get($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = nudgeId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return MapNudge(reader);
    }

    public async Task<NudgeCreated?> CreateAsync(int userId, string title, string description, DateOnly? dueDate, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_nudge_create($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = title },
                new() { Value = description },
                new NpgsqlParameter { Value = dueDate.HasValue ? dueDate.Value : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Date }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new NudgeCreated
        {
            NudgeId = reader.GetInt32(0),
            Title = reader.GetString(1),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(2)
        };
    }

    public async Task<MutationResult?> UpdateAsync(int userId, int nudgeId, string? title, string? description, DateOnly? dueDate, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_nudge_update($1, $2, $3, $4, $5)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = nudgeId },
                new() { Value = (object?)title ?? DBNull.Value },
                new() { Value = (object?)description ?? DBNull.Value },
                new NpgsqlParameter { Value = dueDate.HasValue ? dueDate.Value : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Date }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new MutationResult
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<int> DeleteAsync(int userId, int nudgeId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_nudge_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = nudgeId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }

    public async Task<List<ActionableNudge>> GetActionableAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_nudge_get_actionable($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ActionableNudge>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ActionableNudge
            {
                NudgeId = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                DueDate = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3)
            });
        }
        return results;
    }

    private static Nudge MapNudge(NpgsqlDataReader reader) => new()
    {
        NudgeId = reader.GetInt32(0),
        UserId = reader.GetInt32(1),
        Title = reader.GetString(2),
        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
        DueDate = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
        LastReminded = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
        UpdatedAt = reader.GetFieldValue<DateTimeOffset>(7)
    };
}
