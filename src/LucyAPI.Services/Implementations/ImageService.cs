using LucyAPI.Services.Utilities;
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

    public async Task<ImageResponse> GenerateAsync(int? userId, int? agentId, GenImageRequest request, CancellationToken ct)
    {
        var result = await _gemini.GenerateImageAsync(request.Prompt, request.Model, request.AspectRatio, ct);
        var image = ImageValidator.Validate(result.ImageBytes, MaxGeneratedImageBytes);

        return await StoreAsync(userId, agentId, image, "generated", title: null, description: null,
            request.Prompt, result.ModelUsed, keep: false, ct);
    }

    public async Task<ImageResponse> EditAsync(int userId, int? agentId, EditImageRequest request, CancellationToken ct)
    {
        var (source, _) = await LoadSourceImageAsync(userId, request.ImageId, request.ImageUrl, ct);
        var (bytes, mimeType) = ForGemini(source);

        var result = await _gemini.EditImageAsync(bytes, mimeType, request.Prompt, request.Model, ct);
        var image = ImageValidator.Validate(result.ImageBytes, MaxGeneratedImageBytes);

        return await StoreAsync(userId, agentId, image, "edited", title: null, description: null,
            $"[edit] {request.Prompt}", result.ModelUsed, keep: false, ct);
    }

    public async Task<ImageResponse> UploadAsync(int userId, int? agentId, UploadImageRequest request, CancellationToken ct)
    {
        var image = ImageValidator.Validate(request.Data, ImageValidator.MaxUploadBytes);

        return await StoreAsync(userId, agentId, image, "uploaded", Trim(request.Title), Trim(request.Description),
            prompt: null, model: null, request.Keep, ct);
    }

    public async Task<AnalyzeImageResponse> AnalyzeAsync(int userId, AnalyzeImageRequest request, CancellationToken ct)
    {
        var (source, sourceDesc) = await LoadSourceImageAsync(userId, request.ImageId, request.ImageUrl, ct);

        var (bytes, mimeType) = ForGemini(source);

        var result = await _gemini.AnalyzeImageAsync(bytes, mimeType, request.Prompt, ct);

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

    public async Task<ImageResponse?> GetAsync(int userId, int imageId, CancellationToken ct)
    {
        var record = await _repo.GetAsync(userId, imageId, ct);
        return record is null ? null : ToResponse(record);
    }

    public async Task<ImageResponse?> UpdateKeepAsync(int userId, int imageId, bool keep, CancellationToken ct)
    {
        var record = await _repo.UpdateKeepAsync(userId, imageId, keep, ct);
        return record is null ? null : ToResponse(record);
    }

    public async Task<ImageDeleteResponse> DeleteAsync(int userId, int imageId, bool force, CancellationToken ct)
    {
        var result = await _repo.DeleteAsync(userId, imageId, force, ct);
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

    public async Task<ImageCleanupResponse> CleanupAsync(int userId, CancellationToken ct)
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
        var count = await _repo.DeleteBatchAsync(userId, ids, ct);

        return new ImageCleanupResponse { Deleted = count };
    }

    // -- Helpers --

    /// <summary>Writes the file under a random name with its real extension, then records it. No orphan file if the insert fails.</summary>
    private async Task<ImageResponse> StoreAsync(int? userId, int? agentId, ValidatedImage image, string source,
        string? title, string? description, string? prompt, string? model, bool keep, CancellationToken ct)
    {
        string? notice;
        (image, notice) = ImageValidator.NormalizeForStorage(image);

        var filename = MakeFilename(image.Extension);
        var filepath = Path.Combine(_imagesDir, filename);
        await File.WriteAllBytesAsync(filepath, image.Bytes, ct);

        try
        {
            var record = await _repo.InsertAsync(userId, agentId, filename, source, image.MimeType, title, description,
                prompt, model, keep, image.Bytes.Length, image.Width, image.Height, CancellationToken.None)
                ?? throw new InvalidOperationException("Image insert returned no row");
            var response = ToResponse(record);
            response.Notice = notice;
            return response;
        }
        catch
        {
            try { File.Delete(filepath); } catch (IOException) { }
            throw;
        }
    }

    /// <summary>Gemini takes PNG/JPEG/WebP but not GIF: send a GIF's first frame as PNG.</summary>
    private static (byte[] Bytes, string MimeType) ForGemini(ValidatedImage image) =>
        image.MimeType == "image/gif" ? (ImageValidator.ConvertToPng(image.Bytes), "image/png") : (image.Bytes, image.MimeType);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
        Title = record.Title,
        Description = record.Description,
        MimeType = record.MimeType,
        Source = record.Source,
        AgentId = record.AgentId,
        CreatedAt = record.CreatedAt.ToString("o")
    };

    private const int MaxSourceImageBytes = 20 * 1024 * 1024;

    /// <summary>Gemini output: trusted source, validated only to learn its real type and size.</summary>
    private const int MaxGeneratedImageBytes = 50 * 1024 * 1024;

    private static readonly HttpClient s_publicHttp = new(PublicHttp.CreateHandler(allowRedirects: true, TimeSpan.FromSeconds(5)))
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = MaxSourceImageBytes,
        DefaultRequestHeaders = { { "User-Agent", "LucyAPI/2.0 (+https://lucyapi.snowcapsystems.com)" } }
    };

    private async Task<(ValidatedImage Image, string Description)> LoadSourceImageAsync(
        int userId, int? imageId, string? imageUrl, CancellationToken ct)
    {
        if (imageId.HasValue)
        {
            var record = await _repo.GetAsync(userId, imageId.Value, ct)
                ?? throw new KeyNotFoundException($"Image {imageId.Value} not found");
            var filepath = Path.Combine(_imagesDir, record.Filename);
            if (!File.Exists(filepath))
                throw new KeyNotFoundException($"Image file not found on disk for image_id={imageId.Value}");
            var bytes = await File.ReadAllBytesAsync(filepath, ct);
            return (ImageValidator.Validate(bytes, MaxGeneratedImageBytes), $"image_id={imageId.Value}");
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            // Caller-supplied URL: public https only (SSRF guard enforced on the socket, redirects included)
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("image_url must be an absolute https URL");
            HttpResponseMessage resp;
            try
            {
                resp = await s_publicHttp.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Couldn't fetch image_url: {ex.Message}");
            }
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Couldn't fetch image_url: HTTP {(int)resp.StatusCode}");
            if (resp.Content.Headers.ContentLength > MaxSourceImageBytes)
                throw new InvalidOperationException("image_url is larger than 20 MB");
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > MaxSourceImageBytes)
                throw new InvalidOperationException("image_url is larger than 20 MB");
            return (ImageValidator.Validate(bytes, MaxSourceImageBytes), $"url={imageUrl}");
        }

        throw new InvalidOperationException("Provide either image_id or image_url");
    }

    /// <summary>Random name + the image's real extension. Avoids same-second collisions; not for secrecy (images are public).</summary>
    private static string MakeFilename(string extension) =>
        "img_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant() + extension;
}
