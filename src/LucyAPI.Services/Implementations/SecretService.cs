using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class SecretService(SecretRepository repo) : ISecretService
{
    public Task<List<string>> ListKeysAsync(int userId, CancellationToken ct)
        => repo.ListKeysAsync(userId, ct);

    public Task<Secret?> GetAsync(int userId, string key, CancellationToken ct)
        => repo.GetAsync(userId, key, ct);

    public Task<bool> SetAsync(int userId, string key, SetSecretRequest request, CancellationToken ct)
        => repo.SetAsync(userId, key, request.Value, ct);

    public Task<bool> DeleteAsync(int userId, string key, CancellationToken ct)
        => repo.DeleteAsync(userId, key, ct);
}
