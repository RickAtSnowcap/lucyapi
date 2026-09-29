using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace LucyAPI.Api.OAuth;

/// <summary>
/// Tamper-proof, expiring state carried through the sign-in/consent form as one hidden field.
/// The validated authorization request (client, redirect_uri, PKCE challenge, resource, state) and,
/// after sign-in, the user id are HMAC-signed, so a POST can only complete a request this server
/// validated — nothing in the form can be edited, and stale forms expire.
/// The key is random per process: a restart simply invalidates in-flight sign-in forms.
/// </summary>
public static class SignedState
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    public static string Create(IReadOnlyDictionary<string, string?> values, TimeSpan lifetime)
    {
        var fields = new Dictionary<string, string?>(values)
        {
            ["exp"] = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds().ToString()
        };
        var payload = QueryString.Create(fields).Value ?? "";   // "?a=1&b=2", URL-encoded
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var sig = HMACSHA256.HashData(Key, payloadBytes);
        return OAuthSettings.Base64Url(payloadBytes) + "." + OAuthSettings.Base64Url(sig);
    }

    /// <summary>Returns the fields if the signature is valid and unexpired; otherwise null.</summary>
    public static Dictionary<string, string>? Read(string? blob)
    {
        try
        {
            if (string.IsNullOrEmpty(blob)) return null;
            var dot = blob.IndexOf('.');
            if (dot <= 0) return null;
            var payloadBytes = OAuthSettings.FromBase64Url(blob[..dot]);
            var sig = OAuthSettings.FromBase64Url(blob[(dot + 1)..]);
            if (payloadBytes is null || sig is null) return null;
            if (!CryptographicOperations.FixedTimeEquals(sig, HMACSHA256.HashData(Key, payloadBytes))) return null;

            var parsed = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(payloadBytes));
            var fields = parsed.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            if (!fields.TryGetValue("exp", out var exp) || !long.TryParse(exp, out var expUnix)) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expUnix) return null;
            return fields;
        }
        catch { return null; }
    }
}
