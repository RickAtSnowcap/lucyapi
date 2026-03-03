using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class MemoryRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<Memory>> GetAllAsync(int agentId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_memory_get_all($1)", conn)
        {
            Parameters = { new() { Value = agentId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Memory>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Memory
            {
                Pkid = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
            });
        }
        return results;
    }

    public async Task<Memory?> GetOneAsync(int agentId, int pkid, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_memory_get_one($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Memory
        {
            Pkid = reader.GetInt32(0),
            Title = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
        };
    }

    public async Task<MemoryCreated?> CreateAsync(int agentId, string title, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_memory_create($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = agentId },
                new() { Value = title },
                new() { Value = description }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new MemoryCreated
        {
            Pkid = reader.GetInt32(0),
            Title = reader.GetString(1),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(2)
        };
    }

    public async Task<MutationResult?> UpdateAsync(int agentId, int pkid, string title, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_memory_update($1, $2, $3, $4)", conn)
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
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_memory_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = pkid } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }
}
