namespace LucyAPI.Data.Models;

public sealed class WikiSection
{
    public int SectionId { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string[] Tags { get; set; } = [];
    public bool IsChild { get; set; }
}
