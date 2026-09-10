using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Api.Endpoints;

public static class SaveEndpoints
{
    public static void MapSaveEndpoints(this WebApplication app)
    {
        app.MapGet("/save/{token}", (
            string token,
            string? subject,
            string? content,
            ISaveNotesService service,
            IConfiguration config) =>
        {
            var expectedToken = config["SaveNotes:Token"] ?? "";
            if (string.IsNullOrEmpty(expectedToken) || token != expectedToken)
                return Results.NotFound();

            if (string.IsNullOrEmpty(content))
                return Results.BadRequest(new { detail = "content parameter is required" });

            try
            {
                var result = service.SaveAndEmail(subject ?? "LucyAPI Notes", content);
                return Results.Ok(result);
            }
            catch (InvalidOperationException)
            {
                return Results.StatusCode(429);
            }
        });

        app.MapPost("/save/{token}", (
            string token,
            SaveNotesRequest request,
            ISaveNotesService service,
            IConfiguration config) =>
        {
            var expectedToken = config["SaveNotes:Token"] ?? "";
            if (string.IsNullOrEmpty(expectedToken) || token != expectedToken)
                return Results.NotFound();

            try
            {
                var result = service.SaveAndEmail(request.Subject, request.Content);
                return Results.Ok(result);
            }
            catch (InvalidOperationException)
            {
                return Results.StatusCode(429);
            }
        });
    }
}
