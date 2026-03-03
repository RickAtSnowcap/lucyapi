namespace LucyAPI.Api.Models;

/// <summary>
/// Authenticated agent context, set by ApiKeyAuthMiddleware and stored in HttpContext.Items.
/// </summary>
public sealed class AgentContext
{
    public required int AgentId { get; init; }
    public required string AgentName { get; init; }
    public required int UserId { get; init; }
    public required string UserName { get; init; }
}
