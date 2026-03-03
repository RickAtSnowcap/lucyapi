namespace LucyAPI.Data.Models;

public sealed class HandoffCreated
{
    public int HandoffId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
