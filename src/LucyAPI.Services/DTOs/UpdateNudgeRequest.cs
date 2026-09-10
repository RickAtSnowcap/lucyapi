namespace LucyAPI.Services.DTOs;

public sealed class UpdateNudgeRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool ClearDueDate { get; set; }
}
