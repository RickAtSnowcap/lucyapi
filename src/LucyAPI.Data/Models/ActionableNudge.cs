namespace LucyAPI.Data.Models;

public sealed class ActionableNudge
{
    public int NudgeId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
}
