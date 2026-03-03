namespace LucyAPI.Api.Models;

public sealed class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public string Version { get; set; } = "2.0.0";
}
