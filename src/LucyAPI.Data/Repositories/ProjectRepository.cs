using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed class ProjectRepository(NpgsqlDataSource dataSource)
{
    public async Task<List<Project>> GetAllAsync(int userId, string? statusCode, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_get_all($1, $2)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = (object?)statusCode ?? DBNull.Value }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Project>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadProject(reader));
        }
        return results;
    }

    public async Task<Project?> GetAsync(int projectId, int userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_get($1, $2)", conn)
        {
            Parameters = { new() { Value = projectId }, new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadProject(reader);
    }

    public async Task<ProjectCreated?> CreateAsync(int userId, string title, string description, int statusId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_create($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = userId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = statusId }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ProjectCreated
        {
            ProjectId = reader.GetInt32(0),
            Title = reader.GetString(1),
            Status = reader.GetString(2),
            StatusLabel = reader.GetString(3)
        };
    }

    public async Task<ProjectCreated?> UpdateAsync(int projectId, string title, string description, int statusId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_update($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = projectId },
                new() { Value = title },
                new() { Value = description },
                new() { Value = statusId }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ProjectCreated
        {
            ProjectId = reader.GetInt32(0),
            Title = reader.GetString(1),
            Status = reader.GetString(2),
            StatusLabel = reader.GetString(3)
        };
    }

    public async Task<int> DeleteAsync(int projectId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_delete($1)", conn)
        {
            Parameters = { new() { Value = projectId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return 0;
        return reader.GetInt32(0);
    }

    public async Task<List<ProjectStatus>> GetStatusesAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_project_statuses_get()", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ProjectStatus>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ProjectStatus
            {
                StatusId = reader.GetInt32(0),
                Code = reader.GetString(1),
                Label = reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return results;
    }

    private static Project ReadProject(NpgsqlDataReader reader) => new()
    {
        ProjectId = reader.GetInt32(0),
        Title = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        Status = reader.GetString(3),
        StatusLabel = reader.GetString(4),
        Access = reader.GetString(5),
        PermissionLevel = reader.GetInt32(6)
    };
}
