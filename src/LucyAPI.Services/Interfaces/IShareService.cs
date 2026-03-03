using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IShareService
{
    Task<ShareCreated?> ShareAsync(int userId, ShareObjectRequest request, CancellationToken ct = default);
    Task<bool> RevokeAsync(int userId, int shareId, CancellationToken ct = default);
    Task<List<ShareRecord>> GetSharedByMeAsync(int userId, CancellationToken ct = default);
    Task<List<ShareRecord>> GetSharedToMeAsync(int userId, CancellationToken ct = default);
    Task<SharePermissionCheck> CheckPermissionAsync(int userId, int objectTypeId, int objectId, CancellationToken ct = default);
}
