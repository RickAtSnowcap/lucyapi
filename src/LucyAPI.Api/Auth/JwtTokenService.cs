using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LucyAPI.Api.Auth;

/// <summary>
/// AOT-safe JWT token generation and validation using HMAC-SHA256.
/// No reflection, no NuGet dependencies.
/// </summary>
public sealed class JwtTokenService
{
    private readonly byte[] _signingKey;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;

    // Pre-encoded header: {"alg":"HS256","typ":"JWT"}
    private static readonly string HeaderBase64 = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}"""u8);

    public JwtTokenService(string signingKey, TimeSpan? accessTokenLifetime = null, TimeSpan? refreshTokenLifetime = null)
    {
        _signingKey = Encoding.UTF8.GetBytes(signingKey);
        _accessTokenLifetime = accessTokenLifetime ?? TimeSpan.FromHours(24);
        _refreshTokenLifetime = refreshTokenLifetime ?? TimeSpan.FromDays(7);
    }

    public string CreateAccessToken(int userId, string username, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return CreateToken(userId, username, name, now, now + _accessTokenLifetime);
    }

    public string CreateRefreshToken(int userId, string username, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return CreateToken(userId, username, name, now, now + _refreshTokenLifetime);
    }

    public JwtPayload? ValidateToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        var signatureInput = $"{parts[0]}.{parts[1]}";
        var expectedSignature = ComputeSignature(Encoding.UTF8.GetBytes(signatureInput));
        if (parts[2] != expectedSignature) return null;

        var payloadJson = Base64UrlDecode(parts[1]);
        if (payloadJson is null) return null;

        var payload = JsonSerializer.Deserialize(payloadJson, JwtSerializerContext.Default.JwtPayload);
        if (payload is null) return null;

        if (payload.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return null;

        return payload;
    }

    private string CreateToken(int userId, string username, string name, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var payload = new JwtPayload
        {
            UserId = userId,
            Username = username,
            Name = name,
            Iat = issuedAt.ToUnixTimeSeconds(),
            Exp = expiresAt.ToUnixTimeSeconds()
        };

        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload, JwtSerializerContext.Default.JwtPayload);
        var payloadBase64 = Base64UrlEncode(payloadJson);

        var signatureInput = $"{HeaderBase64}.{payloadBase64}";
        var signature = ComputeSignature(Encoding.UTF8.GetBytes(signatureInput));

        return $"{HeaderBase64}.{payloadBase64}.{signature}";
    }

    private string ComputeSignature(byte[] input)
    {
        var hash = HMACSHA256.HashData(_signingKey, input);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[]? Base64UrlDecode(string input)
    {
        try
        {
            var s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class JwtPayload
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public long Iat { get; set; }
    public long Exp { get; set; }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.SnakeCaseLower)]
[System.Text.Json.Serialization.JsonSerializable(typeof(JwtPayload))]
internal partial class JwtSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
