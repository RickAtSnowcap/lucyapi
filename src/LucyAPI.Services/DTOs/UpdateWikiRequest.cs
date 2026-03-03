namespace LucyAPI.Services.DTOs;

public sealed class UpdateWikiRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
