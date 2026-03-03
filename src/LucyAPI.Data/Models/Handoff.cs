namespace LucyAPI.Data.Models;

public sealed class Handoff
{
    public int HandoffId { get; set; }
    public string Title { get; set; } = "";
    public string? Prompt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
}
