namespace LucyAPI.Services.DTOs;

public sealed class CreateProjectRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int StatusId { get; set; } = 1;
}
