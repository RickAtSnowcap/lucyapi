namespace LucyAPI.Data.Models;

public sealed class SessionCreated
{
    public int SessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}
