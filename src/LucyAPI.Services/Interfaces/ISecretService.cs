using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface ISecretService
{
    Task<List<string>> ListKeysAsync(int userId, CancellationToken ct = default);
    Task<Secret?> GetAsync(int userId, string key, CancellationToken ct = default);
    Task<bool> SetAsync(int userId, string key, SetSecretRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int userId, string key, CancellationToken ct = default);
}
