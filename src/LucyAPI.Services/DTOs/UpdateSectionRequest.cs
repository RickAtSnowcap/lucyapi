namespace LucyAPI.Services.DTOs;

public sealed class UpdateSectionRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? FilePath { get; set; }
}
