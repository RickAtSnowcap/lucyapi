using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IImageService
{
    Task<ImageResponse> GenerateAsync(int? userId, GenImageRequest request, CancellationToken ct = default);
    Task<ImageResponse> EditAsync(int userId, EditImageRequest request, CancellationToken ct = default);
    Task<AnalyzeImageResponse> AnalyzeAsync(int userId, AnalyzeImageRequest request, CancellationToken ct = default);
    Task<List<ImageResponse>> ListAsync(int? userId, bool? keep, int limit, int offset, CancellationToken ct = default);
    Task<ImageResponse?> GetAsync(int userId, int imageId, CancellationToken ct = default);
    Task<ImageResponse?> UpdateKeepAsync(int userId, int imageId, bool keep, CancellationToken ct = default);
    Task<ImageDeleteResponse> DeleteAsync(int userId, int imageId, bool force, CancellationToken ct = default);
    Task<ImageCleanupResponse> CleanupAsync(int userId, CancellationToken ct = default);
}
