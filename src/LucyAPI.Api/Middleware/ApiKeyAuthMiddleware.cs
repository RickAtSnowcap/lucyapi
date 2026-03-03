using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Middleware;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next)
{
    // Paths that don't require authentication
    private static readonly string[] PublicPaths = ["/health", "/time"];

    public async Task InvokeAsync(HttpContext ctx, IAgentService agentService)
    {
        var path = ctx.Request.Path.Value ?? "";

        foreach (var pub in PublicPaths)
        {
            if (path.Equals(pub, StringComparison.OrdinalIgnoreCase))
            {
                await next(ctx);
                return;
            }
        }

        // Check X-Api-Key header first, then agent_key query param
        string? apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = ctx.Request.Query["agent_key"].FirstOrDefault();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(
                new ErrorResponse { Error = "Missing API key", Detail = "Provide X-Api-Key header or agent_key query parameter" },
                AppJsonSerializerContext.Default.ErrorResponse);
            return;
        }

        var agent = await agentService.GetByApiKeyAsync(apiKey, ctx.RequestAborted);
        if (agent is null)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(
                new ErrorResponse { Error = "Invalid API key" },
                AppJsonSerializerContext.Default.ErrorResponse);
            return;
        }

        ctx.SetAgentContext(new AgentContext
        {
            AgentId = agent.AgentId,
            AgentName = agent.AgentName,
            UserId = agent.UserId,
            UserName = agent.UserName
        });

        await next(ctx);
    }
}
