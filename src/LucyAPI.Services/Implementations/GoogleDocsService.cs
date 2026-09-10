using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

/// <summary>
/// Google Docs and Drive service using direct REST API calls (AOT-safe, no Google SDK).
/// OAuth2 credentials loaded from secrets store on first use.
/// </summary>
public sealed class GoogleDocsService : IGoogleDocsService
{
    private readonly ISecretService _secretService;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private string? _refreshToken;
    private string? _clientId;
    private string? _clientSecret;
    private string? _folderId;
    private bool _credentialsLoaded;

    public GoogleDocsService(ISecretService secretService)
    {
        _secretService = secretService;
    }

    private async Task EnsureInitializedAsync(int userId, CancellationToken ct)
    {
        if (_credentialsLoaded && DateTime.UtcNow < _tokenExpiry)
            return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (!_credentialsLoaded)
            {
                _refreshToken = await LoadSecretAsync(userId, "google_oauth_refresh_token", ct);
                _clientId = await LoadSecretAsync(userId, "google_oauth_client_id", ct);
                _clientSecret = await LoadSecretAsync(userId, "google_oauth_client_secret", ct);
                _folderId = await LoadSecretAsync(userId, "google_docs_folder_id", ct);
                _credentialsLoaded = true;
            }

            if (DateTime.UtcNow >= _tokenExpiry)
            {
                await RefreshAccessTokenAsync(ct);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<string> LoadSecretAsync(int userId, string key, CancellationToken ct)
    {
        var secret = await _secretService.GetAsync(userId, key, ct)
            ?? throw new InvalidOperationException($"Secret '{key}' not found");
        return secret.Value!;
    }

    private async Task RefreshAccessTokenAsync(CancellationToken ct)
    {
        using var http = new HttpClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId!,
            ["client_secret"] = _clientSecret!,
            ["refresh_token"] = _refreshToken!,
            ["grant_type"] = "refresh_token"
        });

        var resp = await http.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // refresh 60s early
    }

    private HttpClient CreateAuthedClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return http;
    }

    // -- Docs API --

    public async Task<DocResponse> CreateDocumentAsync(int userId, string title, string? bodyText, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        // Create doc
        var createBody = "{\"title\":\"" + EscapeJson(title) + "\"}";
        var createResp = await http.PostAsync(
            "https://docs.googleapis.com/v1/documents",
            new StringContent(createBody, Encoding.UTF8, "application/json"), ct);
        createResp.EnsureSuccessStatusCode();

        using var createDoc = await JsonDocument.ParseAsync(
            await createResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var docId = createDoc.RootElement.GetProperty("documentId").GetString()!;

        // Move to folder
        await MoveToFolderAsync(http, docId, ct);

        // Insert text if provided
        if (!string.IsNullOrEmpty(bodyText))
        {
            var insertJson = "{\"requests\":[{\"insertText\":{\"location\":{\"index\":1},\"text\":\"" + EscapeJson(bodyText) + "\"}}]}";
            await BatchUpdateAsync(http, docId, insertJson, ct);
        }

        return new DocResponse
        {
            DocumentId = docId,
            Title = title,
            Url = $"https://docs.google.com/document/d/{docId}/edit"
        };
    }

    public async Task<DocResponse> ReadDocumentAsync(int userId, string documentId, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        var resp = await http.GetAsync($"https://docs.googleapis.com/v1/documents/{documentId}", ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var title = doc.RootElement.GetProperty("title").GetString() ?? "";
        var text = ExtractPlainText(doc.RootElement);

        return new DocResponse
        {
            DocumentId = documentId,
            Title = title,
            Text = text,
            Url = $"https://docs.google.com/document/d/{documentId}/edit"
        };
    }

    public async Task<DocResponse> UpdateDocumentAsync(int userId, string documentId, string content, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        // Get current end index
        var endIndex = await GetDocEndIndexAsync(http, documentId, ct);

        // Delete existing content
        if (endIndex > 2)
        {
            var deleteJson = "{\"requests\":[{\"deleteContentRange\":{\"range\":{\"startIndex\":1,\"endIndex\":" + (endIndex - 1) + "}}}]}";
            await BatchUpdateAsync(http, documentId, deleteJson, ct);
        }

        // Insert new content
        if (!string.IsNullOrEmpty(content))
        {
            var insertJson = "{\"requests\":[{\"insertText\":{\"location\":{\"index\":1},\"text\":\"" + EscapeJson(content) + "\"}}]}";
            await BatchUpdateAsync(http, documentId, insertJson, ct);
        }

        return new DocResponse
        {
            DocumentId = documentId,
            Url = $"https://docs.google.com/document/d/{documentId}/edit"
        };
    }

    public async Task<DocResponse> AppendToDocumentAsync(int userId, string documentId, string content, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        var endIndex = await GetDocEndIndexAsync(http, documentId, ct);
        var insertAt = endIndex - 1;

        if (!string.IsNullOrEmpty(content))
        {
            var insertJson = "{\"requests\":[{\"insertText\":{\"location\":{\"index\":" + insertAt + "},\"text\":\"" + EscapeJson(content) + "\"}}]}";
            await BatchUpdateAsync(http, documentId, insertJson, ct);
        }

        return new DocResponse
        {
            DocumentId = documentId,
            Url = $"https://docs.google.com/document/d/{documentId}/edit"
        };
    }

    // -- Drive API --

    public async Task<DriveFileListResponse> ListFilesAsync(int userId, string? folderId, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        var parent = folderId ?? _folderId!;
        var query = Uri.EscapeDataString($"'{parent}' in parents and trashed = false");
        var fields = Uri.EscapeDataString("files(id,name,mimeType,modifiedTime,webViewLink)");

        var resp = await http.GetAsync(
            $"https://www.googleapis.com/drive/v3/files?q={query}&fields={fields}&orderBy=name&pageSize=100", ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var files = new List<DriveFileInfo>();
        if (doc.RootElement.TryGetProperty("files", out var filesArr))
        {
            foreach (var f in filesArr.EnumerateArray())
            {
                files.Add(new DriveFileInfo
                {
                    Id = f.GetProperty("id").GetString() ?? "",
                    Name = f.TryGetProperty("name", out var n) ? n.GetString() : null,
                    MimeType = f.TryGetProperty("mimeType", out var m) ? m.GetString() : null,
                    ModifiedTime = f.TryGetProperty("modifiedTime", out var mt) ? mt.GetString() : null,
                    WebViewLink = f.TryGetProperty("webViewLink", out var w) ? w.GetString() : null
                });
            }
        }

        return new DriveFileListResponse { FolderId = parent, Files = files };
    }

    public async Task<FolderResponse> CreateFolderAsync(int userId, string name, string? parentFolderId, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        var parent = parentFolderId ?? _folderId!;
        var body = "{\"name\":\"" + EscapeJson(name) + "\",\"mimeType\":\"application/vnd.google-apps.folder\",\"parents\":[\"" + parent + "\"]}";

        var resp = await http.PostAsync(
            "https://www.googleapis.com/drive/v3/files?fields=id,name,webViewLink",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return new FolderResponse
        {
            FolderId = doc.RootElement.GetProperty("id").GetString() ?? "",
            Name = doc.RootElement.GetProperty("name").GetString() ?? "",
            Url = doc.RootElement.TryGetProperty("webViewLink", out var w) ? w.GetString() ?? "" : ""
        };
    }

    public async Task<MoveFileResponse> MoveFileAsync(int userId, string fileId, string targetFolderId, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        // Get current parents
        var getResp = await http.GetAsync(
            $"https://www.googleapis.com/drive/v3/files/{fileId}?fields=parents", ct);
        getResp.EnsureSuccessStatusCode();

        using var getDoc = await JsonDocument.ParseAsync(
            await getResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var prevParents = "";
        if (getDoc.RootElement.TryGetProperty("parents", out var parArr))
        {
            var pList = new List<string>();
            foreach (var p in parArr.EnumerateArray())
                pList.Add(p.GetString() ?? "");
            prevParents = string.Join(",", pList);
        }

        // Move
        var req = new HttpRequestMessage(HttpMethod.Patch,
            $"https://www.googleapis.com/drive/v3/files/{fileId}?addParents={targetFolderId}&removeParents={prevParents}&fields=id,name,parents");
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var parents = new List<string>();
        if (doc.RootElement.TryGetProperty("parents", out var newParArr))
        {
            foreach (var p in newParArr.EnumerateArray())
                parents.Add(p.GetString() ?? "");
        }

        return new MoveFileResponse
        {
            FileId = doc.RootElement.GetProperty("id").GetString() ?? "",
            Name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null,
            Parents = parents
        };
    }

    public async Task<DeleteFileResponse> DeleteFileAsync(int userId, string fileId, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        var req = new HttpRequestMessage(HttpMethod.Patch,
            $"https://www.googleapis.com/drive/v3/files/{fileId}");
        req.Content = new StringContent("""{"trashed":true}""", Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        return new DeleteFileResponse { FileId = fileId, Trashed = true };
    }

    public async Task<DriveFileInfo> GetFileMetadataAsync(int userId, string fileId, CancellationToken ct)
    {
        await EnsureInitializedAsync(userId, ct);
        using var http = CreateAuthedClient();

        var fields = Uri.EscapeDataString("id,name,mimeType,modifiedTime,createdTime,size,webViewLink,parents");
        var resp = await http.GetAsync(
            $"https://www.googleapis.com/drive/v3/files/{fileId}?fields={fields}", ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var parents = new List<string>();
        if (root.TryGetProperty("parents", out var parArr))
        {
            foreach (var p in parArr.EnumerateArray())
                parents.Add(p.GetString() ?? "");
        }

        return new DriveFileInfo
        {
            Id = root.GetProperty("id").GetString() ?? "",
            Name = root.TryGetProperty("name", out var n) ? n.GetString() : null,
            MimeType = root.TryGetProperty("mimeType", out var m) ? m.GetString() : null,
            ModifiedTime = root.TryGetProperty("modifiedTime", out var mt) ? mt.GetString() : null,
            CreatedTime = root.TryGetProperty("createdTime", out var ct2) ? ct2.GetString() : null,
            Size = root.TryGetProperty("size", out var s) ? s.GetString() : null,
            WebViewLink = root.TryGetProperty("webViewLink", out var w) ? w.GetString() : null,
            Parents = parents
        };
    }

    // -- Helpers --

    private async Task MoveToFolderAsync(HttpClient http, string docId, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch,
            $"https://www.googleapis.com/drive/v3/files/{docId}?addParents={_folderId}&removeParents=root&fields=id,parents");
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task BatchUpdateAsync(HttpClient http, string docId, string jsonBody, CancellationToken ct)
    {
        var resp = await http.PostAsync(
            $"https://docs.googleapis.com/v1/documents/{docId}:batchUpdate",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<int> GetDocEndIndexAsync(HttpClient http, string documentId, CancellationToken ct)
    {
        var resp = await http.GetAsync($"https://docs.googleapis.com/v1/documents/{documentId}", ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("body", out var body) &&
            body.TryGetProperty("content", out var content))
        {
            var last = content.EnumerateArray().LastOrDefault();
            if (last.ValueKind != JsonValueKind.Undefined &&
                last.TryGetProperty("endIndex", out var endIdx))
            {
                return endIdx.GetInt32();
            }
        }
        return 1;
    }

    private static string ExtractPlainText(JsonElement root)
    {
        var sb = new StringBuilder();
        if (root.TryGetProperty("body", out var body) &&
            body.TryGetProperty("content", out var content))
        {
            foreach (var element in content.EnumerateArray())
            {
                if (element.TryGetProperty("paragraph", out var para))
                {
                    if (para.TryGetProperty("elements", out var elements))
                    {
                        foreach (var elem in elements.EnumerateArray())
                        {
                            if (elem.TryGetProperty("textRun", out var textRun) &&
                                textRun.TryGetProperty("content", out var text))
                            {
                                sb.Append(text.GetString());
                            }
                        }
                    }
                }
            }
        }
        return sb.ToString();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
