using System.Collections.Concurrent;

namespace LucyAPI.Api.OAuth;

/// <summary>
/// In-memory brute-force guard for the /oauth/authorize sign-in form (single LucyAPI instance).
/// A key (client IP, or username) is locked for the rest of its window after too many failures.
/// Also used to rate-limit dynamic client registration per IP.
/// </summary>
public sealed class LoginThrottle(int maxFailures, TimeSpan window)
{
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset WindowStart)> _failures = new();

    public bool IsLocked(string key)
    {
        if (!_failures.TryGetValue(key, out var entry)) return false;
        if (DateTimeOffset.UtcNow - entry.WindowStart > window)
        {
            _failures.TryRemove(key, out _);
            return false;
        }
        return entry.Count >= maxFailures;
    }

    public void RecordFailure(string key)
    {
        var now = DateTimeOffset.UtcNow;
        _failures.AddOrUpdate(key, (1, now), (_, e) =>
            now - e.WindowStart > window ? (1, now) : (e.Count + 1, e.WindowStart));

        // Opportunistic cleanup so the map can't grow without bound.
        if (_failures.Count > 10_000)
            foreach (var kv in _failures)
                if (now - kv.Value.WindowStart > window) _failures.TryRemove(kv.Key, out _);
    }

    public void Reset(string key) => _failures.TryRemove(key, out _);

    /// <summary>Client IP behind Caddy: first X-Forwarded-For entry (Caddy sets it), else the socket peer.</summary>
    public static string ClientIp(HttpContext ctx)
    {
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(xff)) return xff.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
