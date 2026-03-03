namespace LucyAPI.Data.Models;

public sealed class HintCompact
{
    public int Pkid { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
}
