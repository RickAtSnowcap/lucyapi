namespace LucyAPI.Services.DTOs;

public sealed class CreateWikiRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}
