using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class ProjectService(ProjectRepository repo) : IProjectService
{
    public Task<List<Project>> GetAllAsync(int userId, string? statusCode, CancellationToken ct)
        => repo.GetAllAsync(userId, statusCode, ct);

    public Task<Project?> GetAsync(int projectId, int userId, CancellationToken ct)
        => repo.GetAsync(projectId, userId, ct);

    public Task<ProjectCreated?> CreateAsync(int userId, CreateProjectRequest request, CancellationToken ct)
        => repo.CreateAsync(userId, request.Title, request.Description, request.StatusId, ct);

    public Task<ProjectCreated?> UpdateAsync(int projectId, UpdateProjectRequest request, CancellationToken ct)
        => repo.UpdateAsync(projectId, request.Title, request.Description, request.StatusId, ct);

    public Task<int> DeleteAsync(int projectId, CancellationToken ct)
        => repo.DeleteAsync(projectId, ct);

    public Task<List<ProjectStatus>> GetStatusesAsync(CancellationToken ct)
        => repo.GetStatusesAsync(ct);
}
