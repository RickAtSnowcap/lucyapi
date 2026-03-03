namespace LucyAPI.Data.Models;

public sealed class Wiki
{
    public int WikiId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Access { get; set; } = "";
    public int PermissionLevel { get; set; }
}
