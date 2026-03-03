namespace LucyAPI.Data.Models;

public sealed class Session
{
    public int SessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public string? Project { get; set; }
}
