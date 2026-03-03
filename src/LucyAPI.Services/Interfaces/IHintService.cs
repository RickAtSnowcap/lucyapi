using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IHintService
{
    Task<List<Hint>> GetAllAsync(int userId, CancellationToken ct = default);
    Task<List<HintCompact>> GetAllCompactAsync(int userId, CancellationToken ct = default);
    Task<List<Hint>> GetAsync(int pkid, CancellationToken ct = default);
    Task<HintCreated?> CreateCategoryAsync(int userId, CreateHintCategoryRequest request, CancellationToken ct = default);
    Task<HintCreated?> CreateAsync(int userId, CreateHintRequest request, CancellationToken ct = default);
    Task<MutationResult?> UpdateAsync(int pkid, UpdateHintRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int pkid, CancellationToken ct = default);
}
