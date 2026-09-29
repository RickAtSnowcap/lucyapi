using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class SectionService(SectionRepository repo) : ISectionService
{
    public Task<List<ProjectSection>> GetSectionsAsync(int userId, int projectId, CancellationToken ct)
        => repo.GetSectionsAsync(userId, projectId, ct);

    public Task<List<ProjectSectionCompact>> GetSectionsCompactAsync(int userId, int projectId, CancellationToken ct)
        => repo.GetSectionsCompactAsync(userId, projectId, ct);

    public Task<List<ProjectSection>> GetAsync(int userId, int projectId, int sectionId, CancellationToken ct)
        => repo.GetAsync(userId, projectId, sectionId, ct);

    public Task<SectionCreated?> CreateAsync(int userId, int projectId, CreateSectionRequest request, CancellationToken ct)
        => repo.CreateAsync(userId, projectId, request.ParentId, request.Title, request.Description, request.FilePath, ct);

    public Task<SectionCreated?> UpdateAsync(int userId, int projectId, int sectionId, UpdateSectionRequest request, CancellationToken ct)
        => repo.UpdateAsync(userId, projectId, sectionId, request.Title, request.Description, request.FilePath, ct);

    public Task<int> DeleteAsync(int userId, int projectId, int sectionId, CancellationToken ct)
        => repo.DeleteAsync(userId, projectId, sectionId, ct);
}
