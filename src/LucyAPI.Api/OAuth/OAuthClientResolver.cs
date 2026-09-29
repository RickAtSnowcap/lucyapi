using System.Text.Json;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.OAuth;

/// <summary>
/// Resolves a client_id to a usable client:
///   - DCR clients: looked up in oauth_clients.
///   - CIMD clients: the client_id IS an https URL; its JSON metadata document is fetched (cached
///     24h in oauth_clients) and must name itself as client_id and list its redirect_uris.
/// Fetching a URL chosen by the caller is an SSRF risk, so the fetch is https-only, size- and
/// time-limited, follows no redirects, and refuses to CONNECT to any private/loopback/link-local
/// address (checked on the actual socket, which also defeats DNS rebinding).
/// </summary>
public sealed class OAuthClientResolver(OAuthRepository repo)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);
    private const int MaxDocumentBytes = 64 * 1024;

    private static readonly HttpClient Http = new(PublicHttp.CreateHandler(allowRedirects: false, TimeSpan.FromSeconds(4)))
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public static bool IsCimdClientId(string clientId)
        => clientId.StartsWith("https://", StringComparison.Ordinal)
           && Uri.TryCreate(clientId, UriKind.Absolute, out var u) && u.AbsolutePath.Length > 1;

    public async Task<OAuthClient?> ResolveAsync(string? clientId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(clientId) || clientId.Length > 512) return null;

        var cached = await repo.GetClientAsync(clientId, ct);
        if (!IsCimdClientId(clientId)) return cached;   // DCR (or unknown)

        if (cached is not null && cached.MetadataFetchedAt is { } fetched && DateTimeOffset.UtcNow - fetched < CacheFor)
            return cached;

        var fetchedDoc = await FetchMetadataAsync(clientId, ct);
        if (fetchedDoc is null) return cached;   // transient fetch failure: fall back to a stale cache if any
        await repo.UpsertClientAsync(clientId, "cimd", fetchedDoc.Value.Name, fetchedDoc.Value.RedirectUris, ct);
        return await repo.GetClientAsync(clientId, ct);
    }

    private static async Task<(string? Name, string[] RedirectUris)?> FetchMetadataAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.ParseAdd("application/json");
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;
            if (resp.Content.Headers.ContentLength > MaxDocumentBytes) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[MaxDocumentBytes + 1];
            var total = 0;
            int read;
            while (total <= MaxDocumentBytes && (read = await stream.ReadAsync(buffer.AsMemory(total), ct)) > 0)
                total += read;
            if (total > MaxDocumentBytes) return null;

            using var doc = JsonDocument.Parse(buffer.AsMemory(0, total));
            var root = doc.RootElement;
            // The document must identify itself by the exact URL it was fetched from.
            if (!root.TryGetProperty("client_id", out var id) || id.GetString() != url) return null;
            if (!root.TryGetProperty("redirect_uris", out var uris) || uris.ValueKind != JsonValueKind.Array) return null;

            var list = uris.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(OAuthSettings.IsAllowedRedirectUri)
                .ToArray();
            if (list.Length == 0) return null;

            var name = root.TryGetProperty("client_name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            return (name, list);
        }
        catch
        {
            return null;
        }
    }
}
