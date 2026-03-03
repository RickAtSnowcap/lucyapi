using LucyAPI.Api.Models;

namespace LucyAPI.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new HealthResponse()));
    }
}
