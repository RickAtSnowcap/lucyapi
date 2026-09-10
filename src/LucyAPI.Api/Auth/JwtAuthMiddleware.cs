using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;

namespace LucyAPI.Api.Auth;

public sealed class JwtAuthMiddleware(RequestDelegate next)
{
    // Paths that require JWT auth (prefix match)
    private static readonly string[] JwtPrefixes = ["/admin/", "/auth/"];

    // Paths within the JWT scope that are unauthenticated
    private static readonly string[] PublicPaths = ["/auth/login"];

    public async Task InvokeAsync(HttpContext ctx, JwtTokenService jwtService)
    {
        var path = ctx.Request.Path.Value ?? "";

        // Only activate for JWT-scoped paths
        var isJwtPath = false;
        foreach (var prefix in JwtPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                isJwtPath = true;
                break;
            }
        }

        if (!isJwtPath)
        {
            await next(ctx);
            return;
        }

        // Skip auth for public paths (login)
        foreach (var pub in PublicPaths)
        {
            if (path.Equals(pub, StringComparison.OrdinalIgnoreCase))
            {
                await next(ctx);
                return;
            }
        }

        // Extract Bearer token
        var authorization = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(
                new ErrorResponse { Error = "Bearer token required" },
                AppJsonSerializerContext.Default.ErrorResponse);
            return;
        }

        var token = authorization[7..];
        var payload = jwtService.ValidateToken(token);
        if (payload is null)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(
                new ErrorResponse { Error = "Invalid or expired token" },
                AppJsonSerializerContext.Default.ErrorResponse);
            return;
        }

        ctx.SetUserContext(new UserContext
        {
            UserId = payload.UserId,
            Username = payload.Username,
            Name = payload.Name
        });

        await next(ctx);
    }
}
