namespace LucyAPI.Services.DTOs;

public sealed class CreateHintRequest
{
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
