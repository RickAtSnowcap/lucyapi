namespace LucyAPI.Services.DTOs;

public sealed class SessionDescriptionResponse
{
    public int SessionId { get; set; }
    public string StartedAt { get; set; } = "";
    public string? Description { get; set; }
}
