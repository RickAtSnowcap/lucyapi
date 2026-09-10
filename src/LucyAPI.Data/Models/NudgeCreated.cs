namespace LucyAPI.Data.Models;

public sealed class NudgeCreated
{
    public int NudgeId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
