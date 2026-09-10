using System.Security.Cryptography;
using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class ImageService : IImageService
{
    private readonly ImageRepository _repo;
    private readonly IGeminiService _gemini;
    private readonly string _imagesDir;
    private readonly string _baseUrl;

    public ImageService(ImageRepository repo, IGeminiService gemini, string imagesDir, string baseUrl)
    {
        _repo = repo;
        _gemini = gemini;
        _imagesDir = imagesDir;
        _baseUrl = baseUrl;
        Directory.CreateDirectory(_imagesDir);
    }

    public async Task<ImageResponse> GenerateAsync(int? userId, GenImageRequest request, CancellationToken ct)
    {
        var result = await _gemini.GenerateImageAsync(request.Prompt, request.Model, request.AspectRatio, ct);

        var filename = MakeFilename();
        var filepath = Path.Combine(_imagesDir, filename);
        await File.WriteAllBytesAsync(filepath, result.ImageBytes, ct);

        var (width, height) = GetImageDimensions(result.ImageBytes);
        var sizeBytes = result.ImageBytes.Length;

        var record = await _repo.InsertAsync(userId, filename, request.Prompt, result.ModelUsed,
            sizeBytes, width, height, ct);

        return ToResponse(record!);
    }

    public async Task<ImageResponse> EditAsync(int? userId, EditImageRequest request, CancellationToken ct)
    {
        var (sourceBytes, sourceDesc) = await LoadSourceImageAsync(request.ImageId, request.ImageUrl, ct);

        var result = await _gemini.EditImageAsync(sourceBytes, request.Prompt, request.Model, ct);

        var filename = MakeFilename();
        var filepath = Path.Combine(_imagesDir, filename);
        await File.WriteAllBytesAsync(filepath, result.ImageBytes, ct);

        var (width, height) = GetImageDimensions(result.ImageBytes);
        var sizeBytes = result.ImageBytes.Length;
        var editPrompt = $"[edit] {request.Prompt}";

        var record = await _repo.InsertAsync(userId, filename, editPrompt, result.ModelUsed,
            sizeBytes, width, height, ct);

        return ToResponse(record!);
    }

    public async Task<AnalyzeImageResponse> AnalyzeAsync(AnalyzeImageRequest request, CancellationToken ct)
    {
        var (sourceBytes, sourceDesc) = await LoadSourceImageAsync(request.ImageId, request.ImageUrl, ct);

        var result = await _gemini.AnalyzeImageAsync(sourceBytes, request.Prompt, ct);

        return new AnalyzeImageResponse
        {
            Text = result.Text,
            ModelUsed = result.ModelUsed,
            Source = sourceDesc
        };
    }

    public async Task<List<ImageResponse>> ListAsync(int? userId, bool? keep, int limit, int offset,
        CancellationToken ct)
    {
        var records = await _repo.ListAsync(userId, keep, limit, offset, ct);
        return records.Select(ToResponse).ToList();
    }

    public async Task<ImageResponse?> GetAsync(int imageId, CancellationToken ct)
    {
        var record = await _repo.GetAsync(imageId, ct);
        return record is null ? null : ToResponse(record);
    }

    public async Task<ImageResponse?> UpdateKeepAsync(int imageId, bool keep, CancellationToken ct)
    {
        var record = await _repo.UpdateKeepAsync(imageId, keep, ct);
        return record is null ? null : ToResponse(record);
    }

    public async Task<ImageDeleteResponse> DeleteAsync(int imageId, bool force, CancellationToken ct)
    {
        var result = await _repo.DeleteAsync(imageId, force, ct);
        if (result is null)
            return new ImageDeleteResponse { ImageId = imageId, Deleted = false, Detail = "Not found" };

        if (!result.Deleted)
            return new ImageDeleteResponse
            {
                ImageId = imageId,
                Deleted = false,
                Detail = "Image is marked keep=true. Use force=true to delete."
            };

        // Delete file from disk
        var filepath = Path.Combine(_imagesDir, result.Filename);
        try { File.Delete(filepath); } catch (FileNotFoundException) { }

        return new ImageDeleteResponse { ImageId = imageId, Deleted = true };
    }

    public async Task<ImageCleanupResponse> CleanupAsync(int? userId, CancellationToken ct)
    {
        var unkept = await _repo.GetUnkeptAsync(userId, ct);
        if (unkept.Count == 0)
            return new ImageCleanupResponse { Deleted = 0 };

        // Delete files
        foreach (var (_, filename) in unkept)
        {
            var filepath = Path.Combine(_imagesDir, filename);
            try { File.Delete(filepath); } catch (FileNotFoundException) { }
        }

        // Delete DB records
        var ids = unkept.Select(x => x.ImageId).ToArray();
        var count = await _repo.DeleteBatchAsync(ids, ct);

        return new ImageCleanupResponse { Deleted = count };
    }

    // -- Helpers --

    private ImageResponse ToResponse(ImageRecord record) => new()
    {
        ImageId = record.ImageId,
        Url = $"{_baseUrl}/nanoimages/{record.Filename}",
        Filename = record.Filename,
        Prompt = record.Prompt,
        Model = record.Model,
        Keep = record.Keep,
        SizeBytes = record.SizeBytes,
        Width = record.Width,
        Height = record.Height,
        CreatedAt = record.CreatedAt.ToString("o")
    };

    private async Task<(byte[] Bytes, string Description)> LoadSourceImageAsync(
        int? imageId, string? imageUrl, CancellationToken ct)
    {
        if (imageId.HasValue)
        {
            var record = await _repo.GetAsync(imageId.Value, ct)
                ?? throw new InvalidOperationException($"Image {imageId.Value} not found");
            var filepath = Path.Combine(_imagesDir, record.Filename);
            if (!File.Exists(filepath))
                throw new InvalidOperationException($"Image file not found on disk for image_id={imageId.Value}");
            var bytes = await File.ReadAllBytesAsync(filepath, ct);
            return (bytes, $"image_id={imageId.Value}");
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var resp = await http.GetAsync(imageUrl, ct);
            resp.EnsureSuccessStatusCode();
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            return (bytes, $"url={imageUrl}");
        }

        throw new InvalidOperationException("Provide either image_id or image_url");
    }

    private static string MakeFilename()
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"gen_{ts}_{hash}.png";
    }

    private static (int? Width, int? Height) GetImageDimensions(byte[] imageBytes)
    {
        // Simple PNG header parser: width at bytes 16-19, height at bytes 20-23 (big-endian)
        if (imageBytes.Length >= 24 &&
            imageBytes[0] == 0x89 && imageBytes[1] == 0x50 &&
            imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
        {
            var width = (imageBytes[16] << 24) | (imageBytes[17] << 16) |
                        (imageBytes[18] << 8) | imageBytes[19];
            var height = (imageBytes[20] << 24) | (imageBytes[21] << 16) |
                         (imageBytes[22] << 8) | imageBytes[23];
            return (width, height);
        }
        return (null, null);
    }
}
