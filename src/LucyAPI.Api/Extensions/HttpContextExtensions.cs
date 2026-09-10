using LucyAPI.Api.Auth;
using LucyAPI.Api.Models;

namespace LucyAPI.Api.Extensions;

public static class HttpContextExtensions
{
    private const string AgentContextKey = "AgentContext";
    private const string UserContextKey = "UserContext";

    public static void SetAgentContext(this HttpContext ctx, AgentContext agent)
        => ctx.Items[AgentContextKey] = agent;

    public static AgentContext GetAgentContext(this HttpContext ctx)
        => ctx.Items[AgentContextKey] as AgentContext
           ?? throw new InvalidOperationException("AgentContext not set — request did not pass through auth middleware.");

    public static void SetUserContext(this HttpContext ctx, UserContext user)
        => ctx.Items[UserContextKey] = user;

    public static UserContext GetUserContext(this HttpContext ctx)
        => ctx.Items[UserContextKey] as UserContext
           ?? throw new InvalidOperationException("UserContext not set — request did not pass through JWT auth middleware.");
}
