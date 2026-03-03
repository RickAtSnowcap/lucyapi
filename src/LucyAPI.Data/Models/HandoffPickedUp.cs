namespace LucyAPI.Data.Models;

public sealed class HandoffPickedUp
{
    public int HandoffId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset PickedUpAt { get; set; }
}
