using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LucyAPI.Api.OAuth;

namespace LucyAPI.Api.Auth;

/// <summary>
/// Short-lived signed links to a project's or wiki's HTML document, so it can be opened in a phone browser
/// without any credential in the URL. The link names the object, the owning user and an expiry,
/// HMAC-SHA256 signed with a server key (TPM-sealed in Suitcase:DocumentLinkKey). Nothing is stored
/// per link; rotating the key invalidates every outstanding link. Each kind of document signs under its
/// own purpose prefix, so a project link's signature never validates as a wiki link (or the reverse).
/// </summary>
public sealed class DocumentLinkSigner(byte[] key, string baseUrl)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private const string ProjectPurpose = "project-document";
    private const string WikiPurpose = "wiki-document";

    public string CreateProjectUrl(int projectId, int userId) => CreateUrl(ProjectPurpose, "projects", projectId, userId);

    public string CreateWikiUrl(int wikiId, int userId) => CreateUrl(WikiPurpose, "wikis", wikiId, userId);

    /// <summary>True if the signature matches this project + user + expiry and the link hasn't expired.</summary>
    public bool IsValid(int projectId, int userId, long exp, string? sig) => IsValid(ProjectPurpose, projectId, userId, exp, sig);

    /// <summary>True if the signature matches this wiki + user + expiry and the link hasn't expired.</summary>
    public bool IsValidWiki(int wikiId, int userId, long exp, string? sig) => IsValid(WikiPurpose, wikiId, userId, exp, sig);

    private string CreateUrl(string purpose, string route, int objectId, int userId)
    {
        var exp = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var sig = OAuthSettings.Base64Url(Sign(purpose, objectId, userId, exp));
        return $"{baseUrl}/doc/{route}/{objectId}?u={userId}&exp={exp}&sig={sig}";
    }

    private bool IsValid(string purpose, int objectId, int userId, long exp, string? sig)
    {
        try
        {
            if (string.IsNullOrEmpty(sig)) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
            var given = OAuthSettings.FromBase64Url(sig);
            if (given is null) return false;
            return CryptographicOperations.FixedTimeEquals(given, Sign(purpose, objectId, userId, exp));
        }
        catch
        {
            return false;
        }
    }

    // Project payload is unchanged ("project-document\n{project}\n{user}\n{exp}"), so links issued before
    // wiki links existed stay valid.
    private byte[] Sign(string purpose, int objectId, int userId, long exp)
    {
        var payload = string.Create(CultureInfo.InvariantCulture, $"{purpose}\n{objectId}\n{userId}\n{exp}");
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
    }
}
