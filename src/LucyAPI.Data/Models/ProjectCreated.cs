namespace LucyAPI.Data.Models;

public sealed class ProjectCreated
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
}
