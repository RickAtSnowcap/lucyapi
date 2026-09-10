using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using Snowcap.TCrypt;

namespace LucyAPI.Services.Implementations;

public sealed class SecretService(SecretRepository repo, byte[] encryptionKey) : ISecretService
{
    public Task<List<string>> ListKeysAsync(int userId, CancellationToken ct)
        => repo.ListKeysAsync(userId, ct);

    public async Task<Secret?> GetAsync(int userId, string key, CancellationToken ct)
    {
        var secret = await repo.GetAsync(userId, key, ct);
        if (secret is null) return null;
        secret.Value = SuitcaseCrypt.Decrypt(secret.Value!, encryptionKey);
        return secret;
    }

    public Task<bool> SetAsync(int userId, string key, SetSecretRequest request, CancellationToken ct)
    {
        var encrypted = SuitcaseCrypt.Encrypt(request.Value, encryptionKey);
        return repo.SetAsync(userId, key, encrypted, ct);
    }

    public Task<bool> DeleteAsync(int userId, string key, CancellationToken ct)
        => repo.DeleteAsync(userId, key, ct);
}
