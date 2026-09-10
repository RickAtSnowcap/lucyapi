using LucyAPI.Data.Models;
using Npgsql;
using NpgsqlTypes;

namespace LucyAPI.Data.Repositories;

public sealed class HintRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<Hint>> GetAllAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_get_all($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Hint>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Hint
            {
                Pkid = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                HintCategoryId = reader.GetInt32(4),
                SortOrder = reader.GetInt32(5),
                Access = reader.GetString(6),
                PermissionLevel = reader.GetInt16(7)
            });
        }
        return results;
    }

    public async Task<List<HintCompact>> GetAllCompactAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_get_compact($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<HintCompact>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new HintCompact
            {
                Pkid = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return results;
    }

    public async Task<List<Hint>> GetAsync(int pkid, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_get($1)", conn)
        {
            Parameters = { new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Hint>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Hint
            {
                Pkid = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                HintCategoryId = reader.GetInt32(4),
                SortOrder = reader.GetInt32(5),
                UserId = reader.GetInt32(6),
                IsChild = reader.GetBoolean(7)
            });
        }
        return results;
    }

    public async Task<HintCreated?> CreateCategoryAsync(int userId, int parentId, string title, string description, int sortOrder = 0, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_category_create($1, $2, $3, $4, $5)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = parentId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = sortOrder }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new HintCreated
        {
            Pkid = reader.GetInt32(0),
            ParentId = reader.GetInt32(1),
            Title = reader.GetString(2),
            HintCategoryId = reader.GetInt32(3),
            SortOrder = reader.GetInt32(4)
        };
    }

    public async Task<int> DeleteCategoryAsync(int pkid, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_category_delete($1)", conn)
        {
            Parameters = { new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }

    public async Task<HintCreated?> CreateAsync(int userId, int parentId, string title, string description, int sortOrder = 0, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_create($1, $2, $3, $4, $5)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = parentId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = sortOrder }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new HintCreated
        {
            Pkid = reader.GetInt32(0),
            ParentId = reader.GetInt32(1),
            Title = reader.GetString(2),
            HintCategoryId = reader.GetInt32(3),
            SortOrder = reader.GetInt32(4)
        };
    }

    public async Task<MutationResult?> UpdateAsync(int pkid, string? title, string? description, int? sortOrder = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_update($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = pkid },
                new() { Value = (object?)title ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new() { Value = (object?)description ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new() { Value = (object?)sortOrder ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer }
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

    public async Task<int> DeleteAsync(int pkid, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_hint_delete($1)", conn)
        {
            Parameters = { new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }
}
