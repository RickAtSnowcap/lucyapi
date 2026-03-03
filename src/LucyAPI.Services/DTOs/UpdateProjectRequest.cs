namespace LucyAPI.Services.DTOs;

public sealed class UpdateProjectRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int StatusId { get; set; }
}
