using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LucyAPI.Api.OAuth;

namespace LucyAPI.Api.Auth;

/// <summary>
/// Short-lived signed links to a project's HTML document, so it can be opened in a phone browser
/// without any credential in the URL. The link names the project, the owning user and an expiry,
/// HMAC-SHA256 signed with a server key (TPM-sealed in Suitcase:DocumentLinkKey). Nothing is stored
/// per link; rotating the key invalidates every outstanding link.
/// </summary>
public sealed class DocumentLinkSigner(byte[] key, string baseUrl)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public string CreateProjectUrl(int projectId, int userId)
    {
        var exp = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var sig = OAuthSettings.Base64Url(Sign(projectId, userId, exp));
        return $"{baseUrl}/doc/projects/{projectId}?u={userId}&exp={exp}&sig={sig}";
    }

    /// <summary>True if the signature matches this project + user + expiry and the link hasn't expired.</summary>
    public bool IsValid(int projectId, int userId, long exp, string? sig)
    {
        try
        {
            if (string.IsNullOrEmpty(sig)) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
            var given = OAuthSettings.FromBase64Url(sig);
            if (given is null) return false;
            return CryptographicOperations.FixedTimeEquals(given, Sign(projectId, userId, exp));
        }
        catch
        {
            return false;
        }
    }

    private byte[] Sign(int projectId, int userId, long exp)
    {
        var payload = string.Create(CultureInfo.InvariantCulture, $"project-document\n{projectId}\n{userId}\n{exp}");
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
    }
}
