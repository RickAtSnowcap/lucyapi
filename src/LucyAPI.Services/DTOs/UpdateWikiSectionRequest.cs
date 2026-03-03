namespace LucyAPI.Services.DTOs;

public sealed class UpdateWikiSectionRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string[]? Tags { get; set; }
}
