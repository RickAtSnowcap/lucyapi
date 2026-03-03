namespace LucyAPI.Data.Models;

public sealed class MemoryCreated
{
    public int Pkid { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
