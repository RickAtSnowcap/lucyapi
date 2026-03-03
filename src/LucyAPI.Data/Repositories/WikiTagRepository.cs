using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class WikiTagRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<string>> GetTagsAsync(int wikiId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_tags_get($1)", conn)
        {
            Parameters = { new() { Value = wikiId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    public async Task<List<WikiTagSearchResult>> SearchAsync(string tag, int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_wiki_tag_search($1, $2)", conn)
        {
            Parameters = { new() { Value = tag }, new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<WikiTagSearchResult>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new WikiTagSearchResult
            {
                WikiId = reader.GetInt32(0),
                WikiTitle = reader.GetString(1),
                SectionId = reader.GetInt32(2),
                Title = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(5),
                Tags = reader.IsDBNull(6) ? [] : reader.GetFieldValue<string[]>(6)
            });
        }
        return results;
    }
}
