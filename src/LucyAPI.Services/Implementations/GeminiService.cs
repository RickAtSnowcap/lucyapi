using System.Text;
using System.Text.Json;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

/// <summary>
/// Gemini API wrapper for image generation, editing, and analysis.
/// Uses direct REST calls (AOT-safe, no Google GenAI SDK).
/// All request JSON built manually — no reflection-based serialization.
/// </summary>
public sealed class GeminiService : IGeminiService
{
    private readonly string _apiKey;
    private readonly HttpClient _http;

    private static readonly Dictionary<string, string> ModelAliases = new()
    {
        ["nano-banana"] = "gemini-3-pro-image-preview",
        ["nano-banana-pro"] = "gemini-3-pro-image-preview"
    };

    public GeminiService(string apiKey)
    {
        _apiKey = apiKey;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    private static string ResolveModel(string model) =>
        ModelAliases.TryGetValue(model, out var resolved) ? resolved : model;

    public async Task<GeminiImageResult> GenerateImageAsync(string prompt, string model, string aspectRatio,
        CancellationToken ct)
    {
        var modelStr = ResolveModel(model);

        var imageConfigInner = "";
        if (!string.IsNullOrEmpty(aspectRatio) && aspectRatio != "1:1")
            imageConfigInner = "\"aspectRatio\":\"" + EscapeJson(aspectRatio) + "\"";

        var json = "{\"contents\":[{\"parts\":[{\"text\":\"" + EscapeJson(prompt) + "\"}]}],"
            + "\"generationConfig\":{\"responseModalities\":[\"IMAGE\",\"TEXT\"],"
            + "\"imageConfig\":{" + imageConfigInner + "}}}";

        var resp = await _http.PostAsync(
            $"v1beta/models/{modelStr}:generateContent?key={_apiKey}",
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        return await ExtractImageFromResponse(resp, modelStr, ct);
    }

    public async Task<GeminiImageResult> EditImageAsync(byte[] sourceBytes, string prompt, string model,
        CancellationToken ct)
    {
        var modelStr = ResolveModel(model);
        var b64 = Convert.ToBase64String(sourceBytes);

        var json = "{\"contents\":[{\"parts\":["
            + "{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"" + b64 + "\"}},"
            + "{\"text\":\"" + EscapeJson(prompt) + "\"}]}],"
            + "\"generationConfig\":{\"responseModalities\":[\"IMAGE\",\"TEXT\"],"
            + "\"imageConfig\":{}}}";

        var resp = await _http.PostAsync(
            $"v1beta/models/{modelStr}:generateContent?key={_apiKey}",
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        return await ExtractImageFromResponse(resp, modelStr, ct);
    }

    public async Task<GeminiAnalysisResult> AnalyzeImageAsync(byte[] imageBytes, string prompt,
        CancellationToken ct)
    {
        const string modelStr = "gemini-2.0-flash";
        var b64 = Convert.ToBase64String(imageBytes);

        var json = "{\"contents\":[{\"parts\":["
            + "{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"" + b64 + "\"}},"
            + "{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";

        var resp = await _http.PostAsync(
            $"v1beta/models/{modelStr}:generateContent?key={_apiKey}",
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";

        return new GeminiAnalysisResult { Text = text, ModelUsed = modelStr };
    }

    private static async Task<GeminiImageResult> ExtractImageFromResponse(
        HttpResponseMessage resp, string modelStr, CancellationToken ct)
    {
        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var parts = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts");

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("inlineData", out var inlineData))
            {
                var mimeType = inlineData.GetProperty("mimeType").GetString() ?? "";
                if (mimeType.StartsWith("image/"))
                {
                    var data = inlineData.GetProperty("data").GetString() ?? "";
                    return new GeminiImageResult
                    {
                        ImageBytes = Convert.FromBase64String(data),
                        MimeType = mimeType,
                        ModelUsed = modelStr
                    };
                }
            }
        }

        throw new InvalidOperationException("Gemini returned no image in the response");
    }

    private static string EscapeJson(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");
}
