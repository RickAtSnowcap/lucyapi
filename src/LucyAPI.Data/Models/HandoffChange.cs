namespace LucyAPI.Data.Models;

/// <summary>
/// Outcome of an update or delete (fn_handoff_update / fn_handoff_delete). Status: updated | deleted | not_found |
/// not_creator | picked_up. The other fields describe the handoff where the caller may know it (null for not_found).
/// </summary>
public sealed class HandoffChange
{
    public string Status { get; set; } = "";
    public int HandoffId { get; set; }
    public string? Title { get; set; }
    public string? ToAgent { get; set; }
    public string? FromAgent { get; set; }
    public DateTimeOffset? PickedUpAt { get; set; }
}
