using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class WikiRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<Wiki>> GetAllAsync(int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_get_all($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Wiki>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadWiki(reader));
        }
        return results;
    }

    public async Task<Wiki?> GetAsync(int wikiId, int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_get($1, $2)", conn)
        {
            Parameters = { new() { Value = wikiId }, new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadWiki(reader);
    }

    public async Task<WikiCreated?> CreateAsync(int userId, string title, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_create($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = title },
                new() { Value = description }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new WikiCreated
        {
            WikiId = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<WikiCreated?> UpdateAsync(int wikiId, string title, string description, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_update($1, $2, $3)", conn)
        {
            Parameters =
            {
                new() { Value = wikiId },
                new() { Value = title },
                new() { Value = description }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new WikiCreated
        {
            WikiId = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<int> DeleteAsync(int wikiId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_delete($1)", conn)
        {
            Parameters = { new() { Value = wikiId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }

    private static Wiki ReadWiki(NpgsqlDataReader reader) => new()
    {
        WikiId = reader.GetInt32(0),
        Title = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        UpdatedAt = reader.GetFieldValue<DateTimeOffset>(3),
        Access = reader.GetString(4),
        PermissionLevel = reader.GetInt32(5)
    };
}
