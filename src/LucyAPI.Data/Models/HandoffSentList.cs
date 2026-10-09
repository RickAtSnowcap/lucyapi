namespace LucyAPI.Data.Models;

/// <summary>list_sent_handoffs: up to 100 handoffs; Truncated = there are more (older ones not shown).</summary>
public sealed class HandoffSentList
{
    public List<HandoffSent> Handoffs { get; set; } = [];
    public bool Truncated { get; set; }
}
