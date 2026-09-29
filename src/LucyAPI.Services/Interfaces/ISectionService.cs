using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface ISectionService
{
    Task<List<ProjectSection>> GetSectionsAsync(int userId, int projectId, CancellationToken ct = default);
    Task<List<ProjectSectionCompact>> GetSectionsCompactAsync(int userId, int projectId, CancellationToken ct = default);
    Task<List<ProjectSection>> GetAsync(int userId, int projectId, int sectionId, CancellationToken ct = default);
    Task<SectionCreated?> CreateAsync(int userId, int projectId, CreateSectionRequest request, CancellationToken ct = default);
    Task<SectionCreated?> UpdateAsync(int userId, int projectId, int sectionId, UpdateSectionRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int userId, int projectId, int sectionId, CancellationToken ct = default);
}
