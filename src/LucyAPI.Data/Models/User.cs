namespace LucyAPI.Data.Models;

public sealed class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? Email { get; set; }
}
