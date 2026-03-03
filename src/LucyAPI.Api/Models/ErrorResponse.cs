namespace LucyAPI.Api.Models;

public sealed class ErrorResponse
{
    public string Error { get; set; } = "";
    public string? Detail { get; set; }
}
