namespace LucyAPI.Data.Models;

public sealed class Project
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string Access { get; set; } = "";
    public int PermissionLevel { get; set; }
}
