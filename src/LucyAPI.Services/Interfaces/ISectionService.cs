using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface ISectionService
{
    Task<List<ProjectSection>> GetSectionsAsync(int projectId, CancellationToken ct = default);
    Task<List<ProjectSection>> GetAsync(int projectId, int sectionId, CancellationToken ct = default);
    Task<SectionCreated?> CreateAsync(int projectId, CreateSectionRequest request, CancellationToken ct = default);
    Task<SectionCreated?> UpdateAsync(int projectId, int sectionId, UpdateSectionRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int projectId, int sectionId, CancellationToken ct = default);
}
