using LucyAPI.Api.Auth;

namespace LucyAPI.Api.Extensions;

public static class HttpContextExtensions
{
    private const string UserContextKey = "UserContext";

    public static void SetUserContext(this HttpContext ctx, UserContext user)
        => ctx.Items[UserContextKey] = user;

    public static UserContext GetUserContext(this HttpContext ctx)
        => ctx.Items[UserContextKey] as UserContext
           ?? throw new InvalidOperationException("UserContext not set — request did not pass through JWT auth middleware.");
}
