namespace LucyAPI.Data.Models;

public sealed class Memory
{
    public int Pkid { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
