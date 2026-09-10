using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class UserRepository(NpgsqlDataSource dataSource)
{
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, name, username, password_hash, email FROM users WHERE username = $1", conn)
        {
            Parameters = { new() { Value = username } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new User
        {
            UserId = reader.GetInt32(0),
            Name = reader.GetString(1),
            Username = reader.GetString(2),
            PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
            Email = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, name, username, password_hash, email FROM users WHERE user_id = $1", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new User
        {
            UserId = reader.GetInt32(0),
            Name = reader.GetString(1),
            Username = reader.GetString(2),
            PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
            Email = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }

    public async Task<List<User>> ListOtherUsersAsync(int excludeUserId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, name, username, password_hash, email FROM users WHERE user_id != $1 ORDER BY name", conn)
        {
            Parameters = { new() { Value = excludeUserId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<User>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new User
            {
                UserId = reader.GetInt32(0),
                Name = reader.GetString(1),
                Username = reader.GetString(2),
                PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
                Email = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return results;
    }

    public async Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE users SET password_hash = $1 WHERE user_id = $2", conn)
        {
            Parameters = { new() { Value = passwordHash }, new() { Value = userId } }
        };
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
