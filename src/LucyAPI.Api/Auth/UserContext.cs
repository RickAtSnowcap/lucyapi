namespace LucyAPI.Api.Auth;

public sealed class UserContext
{
    public required int UserId { get; init; }
    public required string Username { get; init; }
    public required string Name { get; init; }
}
