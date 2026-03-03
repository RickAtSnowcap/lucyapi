using LucyAPI.Api.Models;

namespace LucyAPI.Api.Extensions;

public static class HttpContextExtensions
{
    private const string AgentContextKey = "AgentContext";

    public static void SetAgentContext(this HttpContext ctx, AgentContext agent)
        => ctx.Items[AgentContextKey] = agent;

    public static AgentContext GetAgentContext(this HttpContext ctx)
        => ctx.Items[AgentContextKey] as AgentContext
           ?? throw new InvalidOperationException("AgentContext not set — request did not pass through auth middleware.");
}
