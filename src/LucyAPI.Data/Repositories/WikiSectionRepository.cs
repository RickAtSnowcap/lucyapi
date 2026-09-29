using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class WikiSectionRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<WikiSection>> GetSectionsAsync(int userId, int wikiId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_get_sections($1, $2)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = wikiId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<WikiSection>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new WikiSection
            {
                SectionId = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(4),
                Tags = reader.IsDBNull(5) ? [] : reader.GetFieldValue<string[]>(5)
            });
        }
        return results;
    }

    public async Task<List<WikiSection>> GetAsync(int userId, int wikiId, int sectionId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_section_get($1, $2, $3)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = wikiId }, new() { Value = sectionId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<WikiSection>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new WikiSection
            {
                SectionId = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(4),
                Tags = reader.IsDBNull(5) ? [] : reader.GetFieldValue<string[]>(5),
                IsChild = reader.GetBoolean(6)
            });
        }
        return results;
    }

    public async Task<WikiSectionCreated?> CreateAsync(int userId, int wikiId, int parentId, string title, string description, string[]? tags, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_section_create($1, $2, $3, $4, $5, $6)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = wikiId },
                new() { Value = parentId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = (object?)tags ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new WikiSectionCreated
        {
            SectionId = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<WikiSectionCreated?> UpdateAsync(int userId, int wikiId, int sectionId, string? title, string? description, string[]? tags, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_section_update($1, $2, $3, $4, $5, $6)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = wikiId },
                new() { Value = sectionId },
                new() { Value = (object?)title ?? DBNull.Value },
                new() { Value = (object?)description ?? DBNull.Value },
                new() { Value = (object?)tags ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new WikiSectionCreated
        {
            SectionId = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<int> DeleteAsync(int userId, int wikiId, int sectionId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_section_delete($1, $2, $3)", conn)
        {
            Parameters = { new() { Value = userId }, new() { Value = wikiId }, new() { Value = sectionId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }
}
