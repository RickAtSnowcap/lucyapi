using LucyAPI.Data.Models;
using Npgsql;
using NpgsqlTypes;

namespace LucyAPI.Data.Repositories;

public sealed class ShareRepository(NpgsqlDataSource dataSource)
{
    public async Task<ShareCreated?> ShareAsync(int userId, int sharedToUserId, int objectTypeId, int objectId, int permissionLevel, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_share_create($1, $2, $3, $4, $5)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = sharedToUserId },
                new() { Value = (short)objectTypeId, NpgsqlDbType = NpgsqlDbType.Smallint },
                new() { Value = objectId },
                new() { Value = (short)permissionLevel, NpgsqlDbType = NpgsqlDbType.Smallint }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ShareCreated
        {
            ShareId = reader.GetInt32(0),
            SharedToUserId = reader.GetInt32(1),
            ObjectTypeId = reader.GetInt16(2),
            ObjectId = reader.GetInt32(3),
            PermissionLevel = reader.GetInt16(4)
        };
    }

    public async Task<bool> RevokeAsync(int userId, int shareId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_share_revoke($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = shareId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return false;
        return reader.GetInt32(0) > 0; // deleted_count
    }

    public async Task<List<ShareRecord>> GetSharedByMeAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_share_list_by_me($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ShareRecord>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ShareRecord
            {
                ShareId = reader.GetInt32(0),
                SharedUserId = reader.GetInt32(1),    // shared_to_user_id
                SharedUsername = reader.GetString(2),  // shared_to_username
                ObjectTypeId = reader.GetInt16(3),
                ObjectTypeName = reader.GetString(4),
                ObjectId = reader.GetInt32(5),
                PermissionLevel = reader.GetInt16(6)
            });
        }
        return results;
    }

    public async Task<List<ShareRecord>> GetSharedToMeAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_share_list_to_me($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ShareRecord>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ShareRecord
            {
                ShareId = reader.GetInt32(0),
                SharedUserId = reader.GetInt32(1),    // shared_by_user_id
                SharedUsername = reader.GetString(2),  // shared_by_username
                ObjectTypeId = reader.GetInt16(3),
                ObjectTypeName = reader.GetString(4),
                ObjectId = reader.GetInt32(5),
                PermissionLevel = reader.GetInt16(6)
            });
        }
        return results;
    }

    public async Task<SharePermissionCheck> CheckPermissionAsync(int userId, int objectTypeId, int objectId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_share_check_permission($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = (short)objectTypeId, NpgsqlDbType = NpgsqlDbType.Smallint },
                new() { Value = objectId }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new SharePermissionCheck { HasAccess = false, PermissionLevel = 0 };
        return new SharePermissionCheck
        {
            HasAccess = reader.GetBoolean(0),
            PermissionLevel = reader.GetInt16(1)
        };
    }
}
