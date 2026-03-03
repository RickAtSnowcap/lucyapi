using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class ShareService(ShareRepository repo) : IShareService
{
    public Task<ShareCreated?> ShareAsync(int userId, ShareObjectRequest request, CancellationToken ct)
        => repo.ShareAsync(userId, request.SharedToUserId, request.ObjectTypeId, request.ObjectId, request.PermissionLevel, ct);

    public Task<bool> RevokeAsync(int userId, int shareId, CancellationToken ct)
        => repo.RevokeAsync(userId, shareId, ct);

    public Task<List<ShareRecord>> GetSharedByMeAsync(int userId, CancellationToken ct)
        => repo.GetSharedByMeAsync(userId, ct);

    public Task<List<ShareRecord>> GetSharedToMeAsync(int userId, CancellationToken ct)
        => repo.GetSharedToMeAsync(userId, ct);

    public Task<SharePermissionCheck> CheckPermissionAsync(int userId, int objectTypeId, int objectId, CancellationToken ct)
        => repo.CheckPermissionAsync(userId, objectTypeId, objectId, ct);
}
