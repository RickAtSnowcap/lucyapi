using System.Text;
using LucyAPI.Data.Models;
using Npgsql;
using NpgsqlTypes;

namespace LucyAPI.Data.Repositories;

public sealed class SecretRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<string>> ListKeysAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_secret_list($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(reader.GetString(1)); // ordinal 1 = key
        }
        return results;
    }

    public async Task<Secret?> GetAsync(int userId, string key, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_secret_get($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = key } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var encryptedBytes = reader.GetFieldValue<byte[]>(2); // ordinal 2 = encrypted_value BYTEA
        return new Secret
        {
            Key = reader.GetString(1), // ordinal 1 = key
            Value = Encoding.UTF8.GetString(encryptedBytes)
        };
    }

    public async Task<bool> SetAsync(int userId, string key, string value, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_secret_set($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = key },
                new() { Value = Encoding.UTF8.GetBytes(value), NpgsqlDbType = NpgsqlDbType.Bytea }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct); // returns secret_id, key on success
    }

    public async Task<bool> DeleteAsync(int userId, string key, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_secret_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = key } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return false;
        return reader.GetInt32(0) > 0; // deleted_count
    }
}
