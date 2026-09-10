namespace LucyAPI.Data.Models;

public sealed class ProjectSectionCompact
{
    public int SectionId { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
}
