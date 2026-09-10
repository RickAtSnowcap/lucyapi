namespace LucyAPI.Services.Interfaces;

public interface IGeminiService
{
    Task<GeminiImageResult> GenerateImageAsync(string prompt, string model, string aspectRatio, CancellationToken ct = default);
    Task<GeminiImageResult> EditImageAsync(byte[] sourceBytes, string prompt, string model, CancellationToken ct = default);
    Task<GeminiAnalysisResult> AnalyzeImageAsync(byte[] imageBytes, string prompt, CancellationToken ct = default);
}

public sealed class GeminiImageResult
{
    public byte[] ImageBytes { get; set; } = [];
    public string MimeType { get; set; } = "";
    public string ModelUsed { get; set; } = "";
}

public sealed class GeminiAnalysisResult
{
    public string Text { get; set; } = "";
    public string ModelUsed { get; set; } = "";
}
