namespace LucyAPI.Api.Middleware;

/// <summary>
/// TEMPORARY (project #62, 2026-09-29 → 2026-10-06): records every use of an agent key so callers that
/// still depend on keys can be found before key auth is retired. Never logs the key itself.
/// Read with: journalctl -u lucyapi --since "2026-09-29" | grep "key-auth:"
/// Remove together with key auth.
/// </summary>
public static class KeyUsageLog
{
    public static void Record(string channel, string result, string? agent, string target, string? from = null)
    {
        try
        {
            Console.Out.WriteLine($"key-auth: channel={channel} result={result} agent={agent ?? "-"} target={target} from={from ?? "-"}");
        }
        catch
        {
            // logging must never throw
        }
    }

    /// <summary>Client address as seen through Caddy (X-Forwarded-For), else the socket peer.</summary>
    public static string ClientAddress(HttpContext ctx)
    {
        try
        {
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded)) return forwarded.Split(',')[0].Trim();
            return ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
        }
        catch
        {
            return "-";
        }
    }
}
