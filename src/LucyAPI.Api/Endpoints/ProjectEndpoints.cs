using LucyAPI.Api.Auth;
using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class ProjectEndpoints
{
    private const string DocumentLinkExpiredHtml =
        "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
        + "<title>Link expired</title></head><body style=\"font-family:Lexend,system-ui,sans-serif;margin:2rem;\">"
        + "<h1>This link has expired or isn't valid</h1><p>Project links last 24 hours. Ask for a fresh one.</p></body></html>";

    public static void MapProjectEndpoints(this WebApplication app)
    {
        app.MapGet("/project-statuses", async (
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var statuses = await projectService.GetStatusesAsync(ct);
            return Results.Ok(statuses);
        });

        app.MapGet("/projects", async (
            string? status,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var projects = await projectService.GetAllAsync(caller.UserId, status, ct);
            return Results.Ok(projects);
        });

        app.MapGet("/projects/{projectId:int}", async (
            int projectId,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            DocumentLinkSigner documentLinks,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            project.DocumentUrl = documentLinks.CreateProjectUrl(projectId, caller.UserId);

            var sections = await sectionService.GetSectionsAsync(projectId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            return Results.Ok(new ProjectDetailResponse { Project = project, Sections = tree });
        });

        app.MapGet("/projects/{projectId:int}/compact", async (
            int projectId,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var project = await projectService.GetCompactAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var sections = await sectionService.GetSectionsCompactAsync(projectId, ct);
            return Results.Ok(new ProjectCompactResponse { Project = project, Sections = sections });
        });

        // Public (see ApiKeyAuthMiddleware): authenticated by the 24h signature from DocumentLinkSigner,
        // so the link works in a phone browser with no credential in the URL.
        app.MapGet("/doc/projects/{projectId:int}", async (
            int projectId,
            int? u,
            long? exp,
            string? sig,
            IProjectService projectService,
            ISectionService sectionService,
            DocumentLinkSigner documentLinks,
            CancellationToken ct) =>
        {
            if (u is null || exp is null || !documentLinks.IsValid(projectId, u.Value, exp.Value, sig))
                return Results.Content(DocumentLinkExpiredHtml, "text/html", statusCode: 403);

            var project = await projectService.GetAsync(projectId, u.Value, ct);
            if (project is null) return Results.NotFound();

            var sections = await sectionService.GetSectionsAsync(projectId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderProjectDocument(project, tree);
            return Results.Content(html, "text/html");
        });

        app.MapPost("/projects", async (
            CreateProjectRequest request,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await projectService.CreateAsync(caller.UserId, request, ct);
            return result is null ? Results.BadRequest() : Results.Created($"/projects/{result.ProjectId}", result);
        });

        app.MapPut("/projects/{projectId:int}", async (
            int projectId,
            UpdateProjectRequest request,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var result = await projectService.UpdateAsync(projectId, request, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapDelete("/projects/{projectId:int}", async (
            int projectId,
            HttpContext ctx,
            IProjectService projectService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var count = await projectService.DeleteAsync(projectId, ct);
            return count >= 0 ? Results.Ok(new SectionsDeletedResponse { SectionsDeleted = count }) : Results.NotFound();
        });
    }
}
