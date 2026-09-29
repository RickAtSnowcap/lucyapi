using LucyAPI.Data.Models;
using Npgsql;

namespace LucyAPI.Data.Repositories;

public sealed record OAuthClient(string ClientId, string RegistrationType, string? ClientName,
    string[] RedirectUris, DateTimeOffset? MetadataFetchedAt);

public sealed record OAuthLoginUser(int UserId, string Name, string PasswordHash);

public sealed record OAuthAgentChoice(int AgentId, string AgentName);

public sealed record OAuthCodeGrant(string ClientId, string RedirectUri, string CodeChallenge,
    string Resource, string? Scope, int UserId, int AgentId);

public sealed record OAuthRotateResult(string Status, int? AgentId, int? UserId, string? Resource, string? Scope);

/// <summary>
/// Data access for the OAuth 2.1 authorization server (project #62). Stored functions only.
/// Token and code values never reach the database — callers pass SHA-256 hashes.
/// </summary>
public sealed class OAuthRepository(NpgsqlDataSource dataSource)
{
    public async Task UpsertClientAsync(string clientId, string registrationType, string? clientName,
        string[] redirectUris, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT lucyapi.fn_oauth_client_upsert($1, $2, $3, $4)", conn)
        {
            Parameters =
            {
                new() { Value = clientId },
                new() { Value = registrationType },
                new() { Value = (object?)clientName ?? DBNull.Value },
                new() { Value = redirectUris }
            }
        };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_oauth_client_get($1)", conn)
        {
            Parameters = { new() { Value = clientId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new OAuthClient(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetFieldValue<string[]>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4));
    }

    public async Task<OAuthLoginUser?> GetUserForLoginAsync(string username, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_oauth_user_for_login($1)", conn)
        {
            Parameters = { new() { Value = username } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new OAuthLoginUser(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task<List<OAuthAgentChoice>> GetAgentsForUserAsync(int userId, CancellationToken ct = default)
    {
        var agents = new List<OAuthAgentChoice>();
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_oauth_agents_for_user($1)", conn)
        {
            Parameters = { new() { Value = userId } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            agents.Add(new OAuthAgentChoice(reader.GetInt32(0), reader.GetString(1)));
        return agents;
    }

    public async Task<bool> CreateCodeAsync(string codeHash, string clientId, string redirectUri, string codeChallenge,
        string resource, string? scope, int userId, int agentId, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT lucyapi.fn_oauth_code_create($1, $2, $3, $4, $5, $6, $7, $8, $9)", conn)
        {
            Parameters =
            {
                new() { Value = codeHash },
                new() { Value = clientId },
                new() { Value = redirectUri },
                new() { Value = codeChallenge },
                new() { Value = resource },
                new() { Value = (object?)scope ?? DBNull.Value },
                new() { Value = userId },
                new() { Value = agentId },
                new() { Value = expiresAt }
            }
        };
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task<OAuthCodeGrant?> ConsumeCodeAsync(string codeHash, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_oauth_code_consume($1)", conn)
        {
            Parameters = { new() { Value = codeHash } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new OAuthCodeGrant(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6));
    }

    public async Task IssueTokensAsync(Guid familyId, string accessHash, string refreshHash, string clientId,
        int userId, int agentId, string resource, string? scope,
        DateTimeOffset accessExpires, DateTimeOffset refreshExpires, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT lucyapi.fn_oauth_tokens_issue($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)", conn)
        {
            Parameters =
            {
                new() { Value = familyId },
                new() { Value = accessHash },
                new() { Value = refreshHash },
                new() { Value = clientId },
                new() { Value = userId },
                new() { Value = agentId },
                new() { Value = resource },
                new() { Value = (object?)scope ?? DBNull.Value },
                new() { Value = accessExpires },
                new() { Value = refreshExpires }
            }
        };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<OAuthRotateResult> RotateRefreshAsync(string refreshHash, string clientId,
        string newAccessHash, string newRefreshHash, DateTimeOffset accessExpires, DateTimeOffset refreshExpires,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT * FROM lucyapi.fn_oauth_refresh_rotate($1, $2, $3, $4, $5, $6)", conn)
        {
            Parameters =
            {
                new() { Value = refreshHash },
                new() { Value = clientId },
                new() { Value = newAccessHash },
                new() { Value = newRefreshHash },
                new() { Value = accessExpires },
                new() { Value = refreshExpires }
            }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new OAuthRotateResult("invalid", null, null, null, null);
        return new OAuthRotateResult(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    public async Task<Agent?> ResolveAccessTokenAsync(string tokenHash, string resource, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT * FROM lucyapi.fn_oauth_access_resolve($1, $2)", conn)
        {
            Parameters = { new() { Value = tokenHash }, new() { Value = resource } }
        };
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Agent
        {
            AgentId = reader.GetInt32(0),
            AgentName = reader.GetString(1),
            UserId = reader.GetInt32(2),
            UserName = reader.GetString(3)
        };
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT lucyapi.fn_oauth_purge_expired()", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
