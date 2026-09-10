using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IImageService
{
    Task<ImageResponse> GenerateAsync(int? userId, GenImageRequest request, CancellationToken ct = default);
    Task<ImageResponse> EditAsync(int? userId, EditImageRequest request, CancellationToken ct = default);
    Task<AnalyzeImageResponse> AnalyzeAsync(AnalyzeImageRequest request, CancellationToken ct = default);
    Task<List<ImageResponse>> ListAsync(int? userId, bool? keep, int limit, int offset, CancellationToken ct = default);
    Task<ImageResponse?> GetAsync(int imageId, CancellationToken ct = default);
    Task<ImageResponse?> UpdateKeepAsync(int imageId, bool keep, CancellationToken ct = default);
    Task<ImageDeleteResponse> DeleteAsync(int imageId, bool force, CancellationToken ct = default);
    Task<ImageCleanupResponse> CleanupAsync(int? userId, CancellationToken ct = default);
}
