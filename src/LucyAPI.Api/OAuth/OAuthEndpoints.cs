using System.Buffers;
using System.Text.Json;
using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace LucyAPI.Api.OAuth;

/// <summary>
/// OAuth 2.1 authorization server for the MCP connector (project #62, Phase 2), per the MCP
/// authorization spec: RFC 9728 protected-resource metadata, RFC 8414 AS metadata, authorization
/// code + PKCE S256, rotating refresh tokens, RFC 7591 dynamic registration (fallback) and Client ID
/// Metadata Documents (preferred by Claude). Public clients only — no client secrets.
/// </summary>
public static class OAuthEndpoints
{
    private static readonly PasswordHasher<string> Hasher = new();
    private static readonly LoginThrottle IpThrottle = new(maxFailures: 10, window: TimeSpan.FromMinutes(15));
    private static readonly LoginThrottle UserThrottle = new(maxFailures: 5, window: TimeSpan.FromMinutes(15));
    private static readonly LoginThrottle RegistrationThrottle = new(maxFailures: 20, window: TimeSpan.FromHours(1));
    private static long _lastPurgeUnix;

    public static void MapOAuthEndpoints(this WebApplication app)
    {
        app.MapGet(OAuthSettings.ProtectedResourceMetadataPath, ProtectedResourceMetadata);
        app.MapGet(OAuthSettings.ProtectedResourceMetadataPath + OAuthSettings.ConnectorPath, ProtectedResourceMetadata);
        app.MapGet(OAuthSettings.AuthorizationServerMetadataPath, AuthorizationServerMetadata);
        app.MapPost("/oauth/register", Register);
        app.MapGet("/oauth/authorize", AuthorizeGet);
        app.MapPost("/oauth/authorize", AuthorizePost).DisableAntiforgery();
        app.MapPost("/oauth/token", Token).DisableAntiforgery();
    }

    // ---------------------------------------------------------------
    //  Discovery
    // ---------------------------------------------------------------

    private static IResult ProtectedResourceMetadata() => Json(w =>
    {
        w.WriteString("resource", OAuthSettings.Resource);
        w.WriteStartArray("authorization_servers"); w.WriteStringValue(OAuthSettings.Issuer); w.WriteEndArray();
        w.WriteStartArray("scopes_supported"); w.WriteStringValue(OAuthSettings.Scope); w.WriteEndArray();
        w.WriteStartArray("bearer_methods_supported"); w.WriteStringValue("header"); w.WriteEndArray();
        w.WriteString("resource_name", "LucyAPI");
    });

    private static IResult AuthorizationServerMetadata() => Json(w =>
    {
        w.WriteString("issuer", OAuthSettings.Issuer);
        w.WriteString("authorization_endpoint", OAuthSettings.Issuer + "/oauth/authorize");
        w.WriteString("token_endpoint", OAuthSettings.Issuer + "/oauth/token");
        w.WriteString("registration_endpoint", OAuthSettings.Issuer + "/oauth/register");
        WriteArray(w, "response_types_supported", "code");
        WriteArray(w, "grant_types_supported", "authorization_code", "refresh_token");
        WriteArray(w, "code_challenge_methods_supported", "S256");
        WriteArray(w, "token_endpoint_auth_methods_supported", "none");
        WriteArray(w, "scopes_supported", OAuthSettings.Scope);
        w.WriteBoolean("client_id_metadata_document_supported", true);
        w.WriteBoolean("authorization_response_iss_parameter_supported", true);
    });

    // ---------------------------------------------------------------
    //  Dynamic Client Registration (RFC 7591) — public clients only
    // ---------------------------------------------------------------

    private static async Task<IResult> Register(HttpContext ctx, OAuthRepository repo)
    {
        var ip = LoginThrottle.ClientIp(ctx);
        if (RegistrationThrottle.IsLocked(ip))
            return OAuthError("slow_down", "Too many registrations; try again later.", 429);
        RegistrationThrottle.RecordFailure(ip);   // counts every registration attempt

        JsonDocument doc;
        try { doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted); }
        catch { return OAuthError("invalid_client_metadata", "Body must be JSON."); }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("redirect_uris", out var uris) || uris.ValueKind != JsonValueKind.Array)
                return OAuthError("invalid_redirect_uri", "redirect_uris is required.");

            var redirectUris = uris.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).ToList();
            if (redirectUris.Count == 0 || redirectUris.Any(u => !OAuthSettings.IsAllowedRedirectUri(u)))
                return OAuthError("invalid_redirect_uri", "Only Claude's callbacks or loopback /callback URIs are allowed.");

            if (root.TryGetProperty("token_endpoint_auth_method", out var am) && am.GetString() is { } method && method != "none")
                return OAuthError("invalid_client_metadata", "Only public clients (token_endpoint_auth_method=none) are supported.");

            var name = root.TryGetProperty("client_name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            if (name is { Length: > 100 }) name = name[..100];

            var clientId = "lucyapi-dcr-" + OAuthSettings.NewOpaqueToken();
            var uriArray = redirectUris.Select(u => u!).ToArray();
            await repo.UpsertClientAsync(clientId, "dcr", name, uriArray, ctx.RequestAborted);

            return Json(w =>
            {
                w.WriteString("client_id", clientId);
                w.WriteNumber("client_id_issued_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (name is not null) w.WriteString("client_name", name);
                WriteArray(w, "redirect_uris", uriArray);
                w.WriteString("token_endpoint_auth_method", "none");
                WriteArray(w, "grant_types", "authorization_code", "refresh_token");
                WriteArray(w, "response_types", "code");
            }, 201);
        }
    }

    // ---------------------------------------------------------------
    //  Authorization endpoint
    // ---------------------------------------------------------------

    private static async Task<IResult> AuthorizeGet(HttpContext ctx, OAuthClientResolver clients)
    {
        var q = ctx.Request.Query;
        var clientId = q["client_id"].ToString();
        var client = await clients.ResolveAsync(clientId, ctx.RequestAborted);
        if (client is null)
            return AuthorizePage.Error("Unknown or unreachable client. Remove the connector and add it again.");

        // redirect_uri problems must NOT redirect (it could be an attacker's URL): show an error page.
        var redirectUri = q["redirect_uri"].ToString();
        if (string.IsNullOrEmpty(redirectUri) && client.RedirectUris.Length == 1) redirectUri = client.RedirectUris[0];
        if (!client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal) || !OAuthSettings.IsAllowedRedirectUri(redirectUri))
            return AuthorizePage.Error("This client's redirect address isn't allowed.");

        var state = q["state"].ToString();
        if (q["response_type"].ToString() != "code")
            return RedirectError(redirectUri, "unsupported_response_type", "Only response_type=code is supported.", state);

        var challenge = q["code_challenge"].ToString();
        if (string.IsNullOrEmpty(challenge) || q["code_challenge_method"].ToString() != "S256")
            return RedirectError(redirectUri, "invalid_request", "PKCE with code_challenge_method=S256 is required.", state);

        var resource = q["resource"].ToString();
        if (!string.IsNullOrEmpty(resource) && !OAuthSettings.IsOurResource(resource))
            return RedirectError(redirectUri, "invalid_target", "Unknown resource.", state);

        var blob = SignedState.Create(new Dictionary<string, string?>
        {
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = redirectUri,
            ["code_challenge"] = challenge,
            ["state"] = state,
        }, OAuthSettings.ConsentFormLifetime);

        return AuthorizePage.SignIn(blob, ClientLabel(client));
    }

    private static async Task<IResult> AuthorizePost(HttpContext ctx, OAuthRepository repo, OAuthClientResolver clients)
    {
        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
        var fields = SignedState.Read(form["st"].ToString());
        if (fields is null)
            return AuthorizePage.Error("This sign-in page expired. Go back to Claude and select Connect again.");

        var client = await clients.ResolveAsync(fields["client_id"], ctx.RequestAborted);
        if (client is null) return AuthorizePage.Error("Unknown client.");
        var redirectUri = fields["redirect_uri"];
        var state = fields.GetValueOrDefault("state") ?? "";

        switch (form["step"].ToString())
        {
            case "signin":
            {
                var ip = LoginThrottle.ClientIp(ctx);
                var username = form["username"].ToString().Trim();
                var userKey = "u:" + username.ToLowerInvariant();
                if (IpThrottle.IsLocked(ip) || UserThrottle.IsLocked(userKey))
                    return AuthorizePage.SignIn(form["st"].ToString(), ClientLabel(client),
                        "Too many failed attempts. Wait 15 minutes and try again.", username);

                var user = username.Length is > 0 and <= 100
                    ? await repo.GetUserForLoginAsync(username, ctx.RequestAborted)
                    : null;
                var ok = user is not null
                         && Hasher.VerifyHashedPassword(username, user.PasswordHash, form["password"].ToString()) != PasswordVerificationResult.Failed;
                if (!ok)
                {
                    IpThrottle.RecordFailure(ip);
                    UserThrottle.RecordFailure(userKey);
                    return AuthorizePage.SignIn(form["st"].ToString(), ClientLabel(client), "Incorrect username or password.", username);
                }
                UserThrottle.Reset(userKey);

                var agents = await repo.GetAgentsForUserAsync(user!.UserId, ctx.RequestAborted);
                if (agents.Count == 0) return AuthorizePage.Error("Your account has no agents to connect.");

                var next = new Dictionary<string, string?>(fields.Where(kv => kv.Key != "exp").ToDictionary(kv => kv.Key, kv => (string?)kv.Value))
                {
                    ["user_id"] = user.UserId.ToString(),
                    ["user_name"] = user.Name,
                };
                return AuthorizePage.ChooseAgent(SignedState.Create(next, OAuthSettings.ConsentFormLifetime),
                    ClientLabel(client), user.Name, agents);
            }

            case "consent":
            {
                if (!fields.TryGetValue("user_id", out var uid) || !int.TryParse(uid, out var userId))
                    return AuthorizePage.Error("This sign-in page expired. Go back to Claude and select Connect again.");

                if (form["decision"].ToString() != "approve")
                    return RedirectError(redirectUri, "access_denied", "The user denied the request.", state);

                if (!int.TryParse(form["agent_id"].ToString(), out var agentId))
                    return AuthorizePage.Error("Choose an agent.");

                var code = OAuthSettings.NewOpaqueToken();
                var created = await repo.CreateCodeAsync(OAuthSettings.Hash(code), client.ClientId, redirectUri,
                    fields["code_challenge"], OAuthSettings.Resource, OAuthSettings.Scope, userId, agentId,
                    DateTimeOffset.UtcNow.Add(OAuthSettings.AuthCodeLifetime), ctx.RequestAborted);
                if (!created) return AuthorizePage.Error("That agent isn't yours to connect.");

                var query = new Dictionary<string, string?> { ["code"] = code, ["iss"] = OAuthSettings.Issuer };
                if (!string.IsNullOrEmpty(state)) query["state"] = state;
                return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, query));
            }

            default:
                return AuthorizePage.Error("Invalid request.");
        }
    }

    // ---------------------------------------------------------------
    //  Token endpoint
    // ---------------------------------------------------------------

    private static async Task<IResult> Token(HttpContext ctx, OAuthRepository repo)
    {
        if (!ctx.Request.HasFormContentType)
            return OAuthError("invalid_request", "Use application/x-www-form-urlencoded.");
        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
        var clientId = form["client_id"].ToString();
        if (string.IsNullOrEmpty(clientId))
            return OAuthError("invalid_client", "client_id is required.", 401);

        var resource = form["resource"].ToString();
        if (!string.IsNullOrEmpty(resource) && !OAuthSettings.IsOurResource(resource))
            return OAuthError("invalid_target", "Unknown resource.");

        await PurgeOccasionallyAsync(repo);

        var now = DateTimeOffset.UtcNow;
        var access = OAuthSettings.NewOpaqueToken();
        var refresh = OAuthSettings.NewOpaqueToken();
        var accessExpires = now.Add(OAuthSettings.AccessTokenLifetime);
        var refreshExpires = now.Add(OAuthSettings.RefreshTokenLifetime);

        switch (form["grant_type"].ToString())
        {
            case "authorization_code":
            {
                var code = form["code"].ToString();
                if (string.IsNullOrEmpty(code)) return OAuthError("invalid_request", "code is required.");
                var grant = await repo.ConsumeCodeAsync(OAuthSettings.Hash(code), ctx.RequestAborted);
                if (grant is null
                    || grant.ClientId != clientId
                    || grant.RedirectUri != form["redirect_uri"].ToString()
                    || !OAuthSettings.VerifyPkce(form["code_verifier"].ToString(), grant.CodeChallenge))
                    return OAuthError("invalid_grant", "The authorization code is invalid, expired, or already used.");

                await repo.IssueTokensAsync(Guid.NewGuid(), OAuthSettings.Hash(access), OAuthSettings.Hash(refresh),
                    clientId, grant.UserId, grant.AgentId, grant.Resource, grant.Scope, accessExpires, refreshExpires,
                    ctx.RequestAborted);
                return TokenResponse(access, refresh, grant.Scope);
            }

            case "refresh_token":
            {
                var presented = form["refresh_token"].ToString();
                if (string.IsNullOrEmpty(presented)) return OAuthError("invalid_request", "refresh_token is required.");
                var rotated = await repo.RotateRefreshAsync(OAuthSettings.Hash(presented), clientId,
                    OAuthSettings.Hash(access), OAuthSettings.Hash(refresh), accessExpires, refreshExpires, ctx.RequestAborted);
                if (rotated.Status != "ok")
                    return OAuthError("invalid_grant", rotated.Status == "reuse_detected"
                        ? "Refresh token reuse detected; this connection was revoked. Reconnect to continue."
                        : "The refresh token is invalid or expired.");
                return TokenResponse(access, refresh, rotated.Scope);
            }

            default:
                return OAuthError("unsupported_grant_type", "Supported: authorization_code, refresh_token.");
        }
    }

    // ---------------------------------------------------------------
    //  Bearer authentication for the MCP connector endpoint
    // ---------------------------------------------------------------

    /// <summary>Resolves "Authorization: Bearer ..." to the bound agent, or null.</summary>
    public static async Task<Agent?> AuthenticateBearerAsync(HttpContext ctx, OAuthRepository repo)
    {
        var header = ctx.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header["Bearer ".Length..].Trim();
        if (token.Length is 0 or > 200) return null;
        return await repo.ResolveAccessTokenAsync(OAuthSettings.Hash(token), OAuthSettings.Resource, ctx.RequestAborted);
    }

    /// <summary>401 that starts (or restarts) Claude's OAuth flow via RFC 9728 discovery.</summary>
    public static async Task WriteChallengeAsync(HttpContext ctx, bool tokenPresented)
    {
        var challenge = $"Bearer resource_metadata=\"{OAuthSettings.Issuer}{OAuthSettings.ProtectedResourceMetadataPath}{OAuthSettings.ConnectorPath}\", scope=\"{OAuthSettings.Scope}\"";
        if (tokenPresented) challenge += ", error=\"invalid_token\"";
        ctx.Response.StatusCode = 401;
        ctx.Response.Headers.WWWAuthenticate = challenge;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(tokenPresented
            ? "{\"error\":\"invalid_token\",\"error_description\":\"Access token is invalid or expired.\"}"
            : "{\"error\":\"unauthorized\",\"error_description\":\"Sign in via OAuth to use this connector.\"}", ctx.RequestAborted);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static IResult TokenResponse(string access, string refresh, string? scope) => Json(w =>
    {
        w.WriteString("access_token", access);
        w.WriteString("token_type", "Bearer");
        w.WriteNumber("expires_in", (int)OAuthSettings.AccessTokenLifetime.TotalSeconds);
        w.WriteString("refresh_token", refresh);
        w.WriteString("scope", scope ?? OAuthSettings.Scope);
    });

    private static IResult RedirectError(string redirectUri, string error, string description, string state)
    {
        var query = new Dictionary<string, string?> { ["error"] = error, ["error_description"] = description, ["iss"] = OAuthSettings.Issuer };
        if (!string.IsNullOrEmpty(state)) query["state"] = state;
        return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, query));
    }

    private static IResult OAuthError(string error, string description, int status = 400)
        => Json(w => { w.WriteString("error", error); w.WriteString("error_description", description); }, status);

    private static string ClientLabel(OAuthClient c)
    {
        if (!string.IsNullOrWhiteSpace(c.ClientName)) return c.ClientName;
        return Uri.TryCreate(c.ClientId, UriKind.Absolute, out var u) ? u.Host : "An application";
    }

    private static async Task PurgeOccasionallyAsync(OAuthRepository repo)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var last = Interlocked.Read(ref _lastPurgeUnix);
        if (now - last < 3600 || Interlocked.CompareExchange(ref _lastPurgeUnix, now, last) != last) return;
        try { await repo.PurgeExpiredAsync(); } catch { /* housekeeping only */ }
    }

    private static void WriteArray(Utf8JsonWriter w, string name, params string[] values)
    {
        w.WriteStartArray(name);
        foreach (var v in values) w.WriteStringValue(v);
        w.WriteEndArray();
    }

    private static IResult Json(Action<Utf8JsonWriter> write, int status = 200) => new JsonWriterResult(write, status);

    private sealed class JsonWriterResult(Action<Utf8JsonWriter> write, int status) : IResult
    {
        public async Task ExecuteAsync(HttpContext ctx)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                write(w);
                w.WriteEndObject();
            }
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers.CacheControl = "no-store";
            await ctx.Response.Body.WriteAsync(buffer.WrittenMemory, ctx.RequestAborted);
        }
    }
}
