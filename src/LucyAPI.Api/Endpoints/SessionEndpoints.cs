using LucyAPI.Api.Extensions;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions", async (
            CreateSessionRequest request,
            HttpContext ctx,
            ISessionService sessionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await sessionService.CreateAsync(caller.AgentId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/sessions/{result.SessionId}", result);
        });

        app.MapGet("/sessions/last", async (
            HttpContext ctx,
            ISessionService sessionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var session = await sessionService.GetLastAsync(caller.AgentId, ct);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });
    }
}
