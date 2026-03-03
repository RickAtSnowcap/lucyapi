namespace LucyAPI.Data.Models;

public sealed class HintCreated
{
    public int Pkid { get; set; }
    public int? ParentId { get; set; }
    public int HintCategoryId { get; set; }
    public string Title { get; set; } = "";
}
