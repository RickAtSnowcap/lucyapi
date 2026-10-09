namespace LucyAPI.Data.Models;

public sealed class Handoff
{
    public int HandoffId { get; set; }
    public string Title { get; set; } = "";
    public string? Prompt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
    /// <summary>Recipient agent name (get only; null in pending lists, where it is the caller).</summary>
    public string? ToAgent { get; set; }
    /// <summary>Creator agent name; null when unknown (created before migration 015, or by an admin).</summary>
    public string? FromAgent { get; set; }
    /// <summary>The creator's last edit; null = never edited. A recipient that already read it can tell it changed.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
