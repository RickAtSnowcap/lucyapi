namespace LucyAPI.Services.DTOs;

public sealed class SaveNotesRequest
{
    public string Subject { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class SaveNotesResponse
{
    public string Status { get; set; } = "";
    public string Filename { get; set; } = "";
    public string To { get; set; } = "";
}
