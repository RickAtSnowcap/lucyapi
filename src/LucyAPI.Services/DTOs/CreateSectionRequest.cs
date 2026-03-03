namespace LucyAPI.Services.DTOs;

public sealed class CreateSectionRequest
{
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? FilePath { get; set; }
}
