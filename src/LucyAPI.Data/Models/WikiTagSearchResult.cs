namespace LucyAPI.Data.Models;

public sealed class WikiTagSearchResult
{
    public int WikiId { get; set; }
    public string WikiTitle { get; set; } = "";
    public int SectionId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string[] Tags { get; set; } = [];
}
