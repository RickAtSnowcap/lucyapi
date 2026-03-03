namespace LucyAPI.Data.Models;

public sealed class Hint
{
    public int Pkid { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int HintCategoryId { get; set; }
    public string? Access { get; set; }
    public int? PermissionLevel { get; set; }
    public int? UserId { get; set; }
    public bool IsChild { get; set; }
}
