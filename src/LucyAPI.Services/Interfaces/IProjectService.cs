using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IProjectService
{
    Task<List<Project>> GetAllAsync(int userId, string? statusCode, CancellationToken ct = default);
    Task<Project?> GetAsync(int projectId, int userId, CancellationToken ct = default);
    Task<ProjectCreated?> CreateAsync(int userId, CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectCreated?> UpdateAsync(int projectId, UpdateProjectRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int projectId, CancellationToken ct = default);
    Task<List<ProjectStatus>> GetStatusesAsync(CancellationToken ct = default);
}
