namespace LucyAPI.Data.Models;

public sealed class ProjectSection
{
    public int SectionId { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? FilePath { get; set; }
    public bool IsChild { get; set; }
}
