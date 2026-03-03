namespace LucyAPI.Data.Models;

public sealed class ProjectStatus
{
    public int StatusId { get; set; }
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
}
