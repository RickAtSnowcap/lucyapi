using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class HandoffRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<Handoff>> ListPendingAsync(int agentId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_list_pending($1)", conn)
        {
            Parameters = { new() { Value = agentId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Handoff>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Handoff
            {
                HandoffId = reader.GetInt32(0),
                Title = reader.GetString(1),
                Prompt = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(3)
            });
        }
        return results;
    }

    public async Task<Handoff?> GetAsync(int agentId, int handoffId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_get($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = handoffId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Handoff
        {
            HandoffId = reader.GetInt32(0),
            Title = reader.GetString(1),
            Prompt = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(3),
            PickedUpAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4)
        };
    }

    public async Task<HandoffCreated?> CreateAsync(int agentId, string title, string prompt, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_create($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = agentId },
                new() { Value = title },
                new() { Value = prompt }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new HandoffCreated
        {
            HandoffId = reader.GetInt32(0),
            Title = reader.GetString(1),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(2)
        };
    }

    public async Task<HandoffPickedUp?> PickupAsync(int agentId, int handoffId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_pickup($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = handoffId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new HandoffPickedUp
        {
            HandoffId = reader.GetInt32(0),
            Title = reader.GetString(1),
            PickedUpAt = reader.GetFieldValue<DateTimeOffset>(2)
        };
    }

    public async Task<int> DeleteAsync(int agentId, int handoffId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = agentId }, new() { Value = handoffId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }
}
