using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class ImageRepository(NpgsqlDataSource dataSource)
{
    public async Task<ImageRecord?> InsertAsync(int? userId, string filename, string? prompt,
        string? model, int sizeBytes, int? width, int? height, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT * FROM lucyapi.fn_image_insert($1, $2, $3, $4, $5, $6, $7)", conn)
        {
            Parameters =
            {
                new() { Value = userId.HasValue ? userId.Value : DBNull.Value },
                new() { Value = filename },
                new() { Value = (object?)prompt ?? DBNull.Value },
                new() { Value = (object?)model ?? DBNull.Value },
                new() { Value = sizeBytes },
                new() { Value = width.HasValue ? width.Value : DBNull.Value },
                new() { Value = height.HasValue ? height.Value : DBNull.Value }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadImageRecord(reader);
    }

    public async Task<ImageRecord?> GetAsync(int imageId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_image_get($1)", conn)
        {
            Parameters = { new() { Value = imageId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadImageRecord(reader);
    }

    public async Task<List<ImageRecord>> ListAsync(int? userId, bool? keep, int limit, int offset,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT * FROM lucyapi.fn_image_list($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = userId.HasValue ? userId.Value : DBNull.Value },
                new() { Value = keep.HasValue ? keep.Value : DBNull.Value },
                new() { Value = limit },
                new() { Value = offset }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ImageRecord>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadImageRecord(reader));
        }
        return results;
    }

    public async Task<ImageRecord?> UpdateKeepAsync(int imageId, bool keep, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT * FROM lucyapi.fn_image_update_keep($1, $2)", conn)
        {
            Parameters = { new() { Value = imageId }, new() { Value = keep } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadImageRecord(reader);
    }

    public async Task<ImageDeleteResult?> DeleteAsync(int imageId, bool force, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT * FROM lucyapi.fn_image_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = imageId }, new() { Value = force } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ImageDeleteResult
        {
            ImageId = reader.GetInt32(0),
            Filename = reader.GetString(1),
            Deleted = reader.GetBoolean(2)
        };
    }

    public async Task<List<(int ImageId, string Filename)>> GetUnkeptAsync(int? userId,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_image_get_unkept($1)", conn)
        {
            Parameters = { new() { Value = userId.HasValue ? userId.Value : DBNull.Value } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<(int, string)>();
        while (await reader.ReadAsync(ct))
        {
            results.Add((reader.GetInt32(0), reader.GetString(1)));
        }
        return results;
    }

    public async Task<int> DeleteBatchAsync(int[] imageIds, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT lucyapi.fn_image_delete_batch($1)", conn)
        {
            Parameters = { new() { Value = imageIds } }
        };
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int count ? count : 0;
    }

    private static ImageRecord ReadImageRecord(NpgsqlDataReader reader) => new()
    {
        ImageId = reader.GetInt32(0),
        Filename = reader.GetString(1),
        Prompt = reader.IsDBNull(2) ? null : reader.GetString(2),
        Model = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(4),
        Keep = reader.GetBoolean(5),
        SizeBytes = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Width = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        Height = reader.IsDBNull(8) ? null : reader.GetInt32(8)
    };
}
