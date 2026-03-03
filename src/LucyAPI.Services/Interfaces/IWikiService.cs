using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IWikiService
{
    Task<List<Wiki>> GetAllAsync(int userId, CancellationToken ct = default);
    Task<Wiki?> GetAsync(int wikiId, int userId, CancellationToken ct = default);
    Task<WikiCreated?> CreateAsync(int userId, CreateWikiRequest request, CancellationToken ct = default);
    Task<WikiCreated?> UpdateAsync(int wikiId, UpdateWikiRequest request, CancellationToken ct = default);
    Task<int> DeleteAsync(int wikiId, CancellationToken ct = default);
}
