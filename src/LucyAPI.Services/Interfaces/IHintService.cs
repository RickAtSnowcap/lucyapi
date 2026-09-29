using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IHintService
{
    Task<List<Hint>> GetAllAsync(int userId, CancellationToken ct = default);
    Task<List<HintCompact>> GetAllCompactAsync(int userId, CancellationToken ct = default);
    Task<List<Hint>> GetAsync(int userId, int pkid, CancellationToken ct = default);
    Task<HintCreated?> CreateCategoryAsync(int userId, CreateHintCategoryRequest request, CancellationToken ct = default);
    Task<HintCreated?> CreateAsync(int userId, CreateHintRequest request, CancellationToken ct = default);
    Task<MutationResult?> UpdateAsync(int userId, int pkid, UpdateHintRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int userId, int pkid, CancellationToken ct = default);
    Task<int> DeleteCategoryAsync(int userId, int pkid, CancellationToken ct = default);
}
