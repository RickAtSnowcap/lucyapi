using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class SectionRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<ProjectSection>> GetSectionsAsync(int projectId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_get_sections($1)", conn)
        {
            Parameters = { new() { Value = projectId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ProjectSection>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ProjectSection
            {
                SectionId = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                FilePath = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return results;
    }

    public async Task<List<ProjectSection>> GetAsync(int projectId, int sectionId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_section_get($1, $2)", conn)
        {
            Parameters = { new() { Value = projectId }, new() { Value = sectionId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ProjectSection>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ProjectSection
            {
                SectionId = reader.GetInt32(0),
                ParentId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                FilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsChild = reader.GetBoolean(5)
            });
        }
        return results;
    }

    public async Task<SectionCreated?> CreateAsync(int projectId, int parentId, string title, string description, string? filePath, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_section_create($1, $2, $3, $4, $5)", conn)
        {
            Parameters =
            {
                new() { Value = projectId },
                new() { Value = parentId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = (object?)filePath ?? DBNull.Value }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new SectionCreated
        {
            SectionId = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<SectionCreated?> UpdateAsync(int projectId, int sectionId, string title, string description, string? filePath, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_section_update($1, $2, $3, $4, $5)", conn)
        {
            Parameters =
            {
                new() { Value = projectId },
                new() { Value = sectionId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = (object?)filePath ?? DBNull.Value }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new SectionCreated
        {
            SectionId = reader.GetInt32(0),
            Title = reader.GetString(1)
        };
    }

    public async Task<int> DeleteAsync(int projectId, int sectionId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_section_delete($1, $2)", conn)
        {
            Parameters = { new() { Value = projectId }, new() { Value = sectionId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }
}
