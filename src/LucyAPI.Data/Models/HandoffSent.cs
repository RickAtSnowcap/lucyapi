namespace LucyAPI.Data.Models;

/// <summary>A handoff the caller created, as listed by list_sent_handoffs (no prompt text).</summary>
public sealed class HandoffSent
{
    public int HandoffId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
    public string ToAgent { get; set; } = "";
}
