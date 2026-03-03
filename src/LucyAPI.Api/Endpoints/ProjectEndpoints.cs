using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using LucyAPI.Services.Utilities;

namespace LucyAPI.Api.Endpoints;

public static class ProjectEndpoints
{
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
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var sections = await sectionService.GetSectionsAsync(projectId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            return Results.Ok(new ProjectDetailResponse { Project = project, Sections = tree });
        });

        app.MapGet("/projects/{projectId:int}/document", async (
            int projectId,
            HttpContext ctx,
            IProjectService projectService,
            ISectionService sectionService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetAgentContext();
            var project = await projectService.GetAsync(projectId, caller.UserId, ct);
            if (project is null) return Results.NotFound();

            var sections = await sectionService.GetSectionsAsync(projectId, ct);
            var tree = TreeBuilder.Build(sections, s => s.SectionId, s => s.ParentId);
            var html = HtmlDocumentRenderer.RenderProjectDocument(tree);
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
