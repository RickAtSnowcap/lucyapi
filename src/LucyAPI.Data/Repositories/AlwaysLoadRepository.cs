using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class AlwaysLoadRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<AlwaysLoadItem>> GetAllAsync(int agentId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_always_load_get_all($1)", conn)
        {
            Parameters = { new() { Value = agentId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<AlwaysLoadItem>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AlwaysLoadItem
            {
                Pkid = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return results;
    }

    public async Task<List<AlwaysLoadItem>> GetItemAsync(int agentId, int pkid, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_always_load_get_item($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<AlwaysLoadItem>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AlwaysLoadItem
            {
                Pkid = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsChild = reader.GetBoolean(4)
            });
        }
        return results;
    }

    public async Task<AlwaysLoadCreated?> CreateAsync(int agentId, int parentId, string title, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_always_load_create($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = agentId },
                new() { Value = parentId },
                new() { Value = title },
                new() { Value = description }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new AlwaysLoadCreated
        {
            Pkid = reader.GetInt32(0),
            ParentId = reader.GetInt32(1),
            Title = reader.GetString(2)
        };
    }

    public async Task<MutationResult?> UpdateAsync(int agentId, int pkid, string title, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_always_load_update($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = agentId },
                new() { Value = pkid },
                new() { Value = title },
                new() { Value = description }
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

    public async Task<int> DeleteAsync(int agentId, int pkid, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_always_load_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }
}
