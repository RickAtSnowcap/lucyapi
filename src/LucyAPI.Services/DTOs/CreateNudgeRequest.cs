namespace LucyAPI.Services.DTOs;

public sealed class CreateNudgeRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly? DueDate { get; set; }
}
