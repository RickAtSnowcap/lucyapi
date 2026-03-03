using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class SectionService(SectionRepository repo) : ISectionService
{
    public Task<List<ProjectSection>> GetSectionsAsync(int projectId, CancellationToken ct)
        => repo.GetSectionsAsync(projectId, ct);

    public Task<List<ProjectSection>> GetAsync(int projectId, int sectionId, CancellationToken ct)
        => repo.GetAsync(projectId, sectionId, ct);

    public Task<SectionCreated?> CreateAsync(int projectId, CreateSectionRequest request, CancellationToken ct)
        => repo.CreateAsync(projectId, request.ParentId, request.Title, request.Description, request.FilePath, ct);

    public Task<SectionCreated?> UpdateAsync(int projectId, int sectionId, UpdateSectionRequest request, CancellationToken ct)
        => repo.UpdateAsync(projectId, sectionId, request.Title, request.Description, request.FilePath, ct);

    public Task<int> DeleteAsync(int projectId, int sectionId, CancellationToken ct)
        => repo.DeleteAsync(projectId, sectionId, ct);
}
