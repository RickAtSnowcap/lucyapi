using LucyAPI.Api.Extensions;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class ContextEndpoints
{
    public static void MapContextEndpoints(this WebApplication app)
    {
        app.MapGet("/agents/{agentName}/context", async (
            string agentName,
            HttpContext ctx,
            IAgentService agentService,
            IContextService contextService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var target = await agentService.GetByNameAsync(agentName, ct);
            if (target is null) return Results.NotFound();

            var context = await contextService.GetFullAsync(target.AgentId, target.UserId, ct);
            if (context is null) return Results.NotFound();

            return Results.Ok(context);
        });
    }
}
