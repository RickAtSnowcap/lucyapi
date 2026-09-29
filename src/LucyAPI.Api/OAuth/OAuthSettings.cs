using System.Security.Cryptography;
using System.Text;

namespace LucyAPI.Api.OAuth;

/// <summary>
/// Fixed parameters of LucyAPI's OAuth 2.1 authorization server (project #62) plus the small
/// crypto helpers everything else shares. LucyAPI is both the authorization server (AS) and the
/// resource server (RS) for the MCP connector endpoint.
/// </summary>
public static class OAuthSettings
{
    public const string Issuer = "https://lucyapi.snowcapsystems.com";
    public const string ConnectorPath = "/mcp/connector";
    public const string Resource = Issuer + ConnectorPath;
    public const string Scope = "lucyapi";

    public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";
    public const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);   // sliding: renewed on each rotation
    public static readonly TimeSpan AuthCodeLifetime = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ConsentFormLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Claude web/desktop/mobile callbacks (claude.com is the announced future host).</summary>
    private static readonly string[] AllowedHostedRedirects =
    [
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
    ];

    /// <summary>
    /// Redirect URIs we will ever send an authorization code to: Claude's hosted callbacks, or a
    /// loopback callback on any port (Claude Code runs a local listener). Everything else is refused
    /// even if a client registers it — a code must never be delivered to an arbitrary site.
    /// </summary>
    public static bool IsAllowedRedirectUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;
        if (AllowedHostedRedirects.Contains(uri, StringComparer.Ordinal)) return true;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        return u.Scheme == Uri.UriSchemeHttp
            && (u.Host == "localhost" || u.Host == "127.0.0.1")
            && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment)
            && u.AbsolutePath == "/callback";
    }

    /// <summary>
    /// Does the requested redirect_uri match one the client registered? Exact match, or — for loopback
    /// redirects — same scheme, host and path with ANY port (RFC 8252 §7.3: native apps such as Claude
    /// Code listen on an ephemeral port, so their metadata registers "http://localhost/callback" and they
    /// arrive with "http://localhost:54321/callback"). The allowlist check still applies separately.
    /// </summary>
    public static bool RedirectUriMatches(IEnumerable<string> registered, string? requested)
    {
        if (string.IsNullOrEmpty(requested)) return false;
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var req)) return false;
        foreach (var r in registered)
        {
            if (string.Equals(r, requested, StringComparison.Ordinal)) return true;
            if (!Uri.TryCreate(r, UriKind.Absolute, out var reg)) continue;
            var loopback = reg.Scheme == Uri.UriSchemeHttp && (reg.Host == "localhost" || reg.Host == "127.0.0.1");
            if (loopback
                && req.Scheme == reg.Scheme
                && req.Host == reg.Host
                && req.AbsolutePath == reg.AbsolutePath
                && string.IsNullOrEmpty(req.Query) && string.IsNullOrEmpty(req.Fragment))
                return true;
        }
        return false;
    }

    /// <summary>The resource indicator must name this connector (trailing slash tolerated).</summary>
    public static bool IsOurResource(string? resource)
        => resource is not null && resource.TrimEnd('/') == Resource;

    /// <summary>32 random bytes, base64url — used for codes, access and refresh tokens, client ids.</summary>
    public static string NewOpaqueToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>What the database stores instead of a token: lowercase hex SHA-256.</summary>
    public static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>PKCE S256: BASE64URL(SHA256(ASCII(code_verifier))) == code_challenge.</summary>
    public static bool VerifyPkce(string? codeVerifier, string codeChallenge)
    {
        if (string.IsNullOrEmpty(codeVerifier) || codeVerifier.Length is < 43 or > 128) return false;
        var computed = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(codeChallenge));
    }

    public static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[]? FromBase64Url(string s)
    {
        try
        {
            var b = s.Replace('-', '+').Replace('_', '/');
            b = b.PadRight(b.Length + (4 - b.Length % 4) % 4, '=');
            return Convert.FromBase64String(b);
        }
        catch { return null; }
    }
}
