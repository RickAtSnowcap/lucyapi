using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class BootEndpoints
{
    public static void MapBootEndpoints(this WebApplication app)
    {
        app.MapGet("/boot", async (
            string? agent_key,
            HttpContext ctx,
            IAlwaysLoadService alwaysLoadService,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var agentName = caller.AgentName;

            var items = await alwaysLoadService.GetAllAsync(caller.AgentId, ct);
            var tree = TreeBuilder.Build(items, i => i.Pkid, i => i.ParentId);

            const string baseUrl = "https://lucyapi.snowcapsystems.com";
            var keySuffix = !string.IsNullOrEmpty(agent_key) ? $"agent_key={agent_key}" : "";

            string Url(string path, bool hasParams = false)
            {
                if (string.IsNullOrEmpty(keySuffix)) return $"{baseUrl}{path}";
                var sep = hasParams ? "&" : "?";
                return $"{baseUrl}{path}{sep}{keySuffix}";
            }

            var saveToken = config["SaveNotes:Token"] ?? "";

            var endpoints = new BootEndpointMap
            {
                Time = $"{baseUrl}/time",
                Context = Url($"/agents/{agentName}/context"),
                AlwaysLoad = Url($"/agents/{agentName}/context/always_load"),
                AlwaysLoadItem = Url($"/agents/{agentName}/context/always_load/{{pkid}}"),
                Memories = Url($"/agents/{agentName}/memories"),
                MemoryItem = Url($"/agents/{agentName}/memories/{{pkid}}"),
                Preferences = Url($"/agents/{agentName}/preferences"),
                PreferenceItem = Url($"/agents/{agentName}/preferences/{{pkid}}"),
                Projects = Url("/projects"),
                ProjectItem = Url("/projects/{project_id}"),
                ProjectSection = Url("/projects/{project_id}/sections/{section_id}"),
                ProjectDocument = Url("/projects/{project_id}/document"),
                SessionStart = Url("/sessions"),
                SessionLast = Url("/sessions/last"),
                Save = new BootSaveEndpoint
                {
                    Url = $"{baseUrl}/save/{saveToken}",
                    Usage = "POST JSON {subject, content} or GET with ?subject=...&content=... (URL-encoded). Emails markdown attachment to Rick."
                }
            };

            return Results.Ok(new BootResponse
            {
                Endpoints = endpoints,
                Agent = agentName,
                AlwaysLoad = tree
            });
        });
    }
}
