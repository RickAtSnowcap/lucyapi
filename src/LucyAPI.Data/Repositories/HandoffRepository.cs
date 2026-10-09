using LucyAPI.Data.Models;
using Npgsql;
using NpgsqlTypes;

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
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(3),
                FromAgent = reader.IsDBNull(4) ? null : reader.GetString(4),
                UpdatedAt = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)
            });
        }
        return results;
    }

    public async Task<Handoff?> GetAsync(int callerAgentId, int handoffId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_get($1, $2)", conn)
        {
            Parameters = { new() { Value = callerAgentId }, new() { Value = handoffId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Handoff
        {
            HandoffId = reader.GetInt32(0),
            Title = reader.GetString(1),
            Prompt = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(3),
            PickedUpAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            ToAgent = reader.GetString(5),
            FromAgent = reader.IsDBNull(6) ? null : reader.GetString(6),
            UpdatedAt = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7)
        };
    }

    /// <param name="agentId">The recipient.</param>
    /// <param name="createdByAgentId">The sending agent; null when not an agent (admin).</param>
    public async Task<HandoffCreated?> CreateAsync(int agentId, string title, string prompt, int? createdByAgentId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_create($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = agentId },
                new() { Value = title },
                new() { Value = prompt },
                new() { Value = (object?)createdByAgentId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer }
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

    /// <summary>Recipient: any time. Creator: only while pending. See fn_handoff_delete for the statuses.</summary>
    public async Task<HandoffChange> DeleteAsync(int callerAgentId, int handoffId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = callerAgentId }, new() { Value = handoffId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new HandoffChange { Status = "not_found", HandoffId = handoffId };
        return new HandoffChange
        {
            Status = reader.GetString(0),
            HandoffId = reader.GetInt32(1),
            Title = reader.IsDBNull(2) ? null : reader.GetString(2),
            ToAgent = reader.IsDBNull(3) ? null : reader.GetString(3),
            PickedUpAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4)
        };
    }

    /// <summary>Creator only, while pending; null title/prompt = keep. See fn_handoff_update for the statuses.</summary>
    public async Task<HandoffChange> UpdateAsync(int callerAgentId, int handoffId, string? title, string? prompt, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_update($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = callerAgentId },
                new() { Value = handoffId },
                new() { Value = (object?)title ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new() { Value = (object?)prompt ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new HandoffChange { Status = "not_found", HandoffId = handoffId };
        return new HandoffChange
        {
            Status = reader.GetString(0),
            HandoffId = reader.GetInt32(1),
            Title = reader.IsDBNull(2) ? null : reader.GetString(2),
            ToAgent = reader.IsDBNull(3) ? null : reader.GetString(3),
            FromAgent = reader.IsDBNull(4) ? null : reader.GetString(4),
            PickedUpAt = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)
        };
    }

    /// <summary>Handoffs the caller created, newest first: at most 100, Truncated when there are more.</summary>
    public async Task<HandoffSentList> ListSentAsync(int callerAgentId, bool pendingOnly, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_handoff_list_sent($1, $2)", conn)
        {
            Parameters = { new() { Value = callerAgentId }, new() { Value = pendingOnly } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<HandoffSent>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new HandoffSent
            {
                HandoffId = reader.GetInt32(0),
                Title = reader.GetString(1),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(2),
                PickedUpAt = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                ToAgent = reader.GetString(4),
                UpdatedAt = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)
            });
        }
        // the function returns up to 101 rows: the 101st only says there are more
        var truncated = results.Count > 100;
        if (truncated) results.RemoveRange(100, results.Count - 100);
        return new HandoffSentList { Handoffs = results, Truncated = truncated };
    }
}
