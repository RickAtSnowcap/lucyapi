using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class SecretEndpoints
{
    public static void MapSecretEndpoints(this WebApplication app)
    {
        app.MapGet("/secrets", async (
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var keys = await secretService.ListKeysAsync(caller.UserId, ct);
            return Results.Ok(new SecretKeyListResponse { Keys = keys });
        });

        app.MapGet("/secrets/{key}", async (
            string key,
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var secret = await secretService.GetAsync(caller.UserId, key, ct);
            return secret is null ? Results.NotFound() : Results.Ok(secret);
        });

        app.MapPut("/secrets/{key}", async (
            string key,
            SetSecretRequest request,
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var success = await secretService.SetAsync(caller.UserId, key, request, ct);
            return success ? Results.Ok(new KeyStatusResponse { Key = key, Status = "saved" }) : Results.BadRequest();
        });

        app.MapDelete("/secrets/{key}", async (
            string key,
            HttpContext ctx,
            ISecretService secretService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var success = await secretService.DeleteAsync(caller.UserId, key, ct);
            return success ? Results.Ok(new KeyStatusResponse { Key = key, Status = "deleted" }) : Results.NotFound();
        });
    }
}
