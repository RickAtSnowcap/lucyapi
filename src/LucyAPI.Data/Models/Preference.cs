namespace LucyAPI.Data.Models;

public sealed class Preference
{
    public int Pkid { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool IsChild { get; set; }
}
