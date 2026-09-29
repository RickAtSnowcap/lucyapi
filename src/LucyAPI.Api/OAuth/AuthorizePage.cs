using System.Net;
using System.Text;
using LucyAPI.Data.Repositories;

namespace LucyAPI.Api.OAuth;

/// <summary>
/// Server-rendered HTML for /oauth/authorize: step 1 sign in, step 2 choose ONE agent and approve.
/// Snowcap themes (dark default, light via prefers-color-scheme), Lexend. Every interpolated value is
/// HTML-encoded. Responses are marked no-store and unframable (clickjacking guard).
/// </summary>
public static class AuthorizePage
{
    public static IResult SignIn(string stateBlob, string clientLabel, string? error = null, string? username = null)
    {
        var body = new StringBuilder();
        body.Append("<h1>Connect to LucyAPI</h1>");
        body.Append("<p class=\"sub\"><strong>").Append(E(clientLabel)).Append("</strong> wants to connect to LucyAPI. Sign in to continue.</p>");
        if (error is not null) body.Append("<p class=\"err\">").Append(E(error)).Append("</p>");
        body.Append("<form method=\"post\" action=\"/oauth/authorize\" autocomplete=\"on\">")
            .Append("<input type=\"hidden\" name=\"step\" value=\"signin\">")
            .Append("<input type=\"hidden\" name=\"st\" value=\"").Append(E(stateBlob)).Append("\">")
            .Append("<label>Username<input name=\"username\" autocomplete=\"username\" required value=\"").Append(E(username ?? "")).Append("\"></label>")
            .Append("<label>Password<input name=\"password\" type=\"password\" autocomplete=\"current-password\" required></label>")
            .Append("<button type=\"submit\">Sign in</button>")
            .Append("</form>");
        return Page(body.ToString());
    }

    public static IResult ChooseAgent(string stateBlob, string clientLabel, string userName, IReadOnlyList<OAuthAgentChoice> agents)
    {
        var body = new StringBuilder();
        body.Append("<h1>Choose an agent</h1>");
        body.Append("<p class=\"sub\">Signed in as <strong>").Append(E(userName)).Append("</strong>. ")
            .Append("<strong>").Append(E(clientLabel)).Append("</strong> will act as the agent you choose — and only that agent.</p>");
        body.Append("<form method=\"post\" action=\"/oauth/authorize\">")
            .Append("<input type=\"hidden\" name=\"step\" value=\"consent\">")
            .Append("<input type=\"hidden\" name=\"st\" value=\"").Append(E(stateBlob)).Append("\">")
            .Append("<fieldset>");
        for (var i = 0; i < agents.Count; i++)
        {
            body.Append("<label class=\"choice\"><input type=\"radio\" name=\"agent_id\" value=\"").Append(agents[i].AgentId).Append('"')
                .Append(agents.Count == 1 ? " checked" : "").Append(" required> ")
                .Append(E(agents[i].AgentName)).Append("</label>");
        }
        body.Append("</fieldset>")
            .Append("<p class=\"note\">Access lasts while it's in use and ends after 30 days idle. It can be revoked at any time.</p>")
            .Append("<div class=\"row\"><button type=\"submit\" name=\"decision\" value=\"approve\">Approve</button>")
            .Append("<button type=\"submit\" name=\"decision\" value=\"deny\" class=\"secondary\" formnovalidate>Deny</button></div>")
            .Append("</form>");
        return Page(body.ToString());
    }

    public static IResult Error(string message, int statusCode = 400)
        => Page("<h1>Can't connect</h1><p class=\"err\">" + E(message) + "</p>", statusCode);

    private static IResult Page(string body, int statusCode = 200)
    {
        var html = """
<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LucyAPI — Connect</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Lexend:wght@400;600&display=swap" rel="stylesheet">
<style>
:root{--bg:#141414;--surface:#1C1C1C;--input:#1A1A1A;--border:#3A3A3A;--text:#E8E8E8;--text2:#9A9A9A;--accent:#F5B731;--accent-h:#F7C44E;--on-accent:#141414;--error:#F87171;--error-bg:rgba(248,113,113,.12)}
@media (prefers-color-scheme: light){:root{--bg:#F8FAFB;--surface:#EFF3F5;--input:#FFFFFF;--border:#C8D1D6;--text:#151B1E;--text2:#5A6872;--accent:#4A6FA5;--accent-h:#3D5F92;--on-accent:#FFFFFF;--error:#DC2626;--error-bg:rgba(220,38,38,.08)}}
*{box-sizing:border-box}body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:var(--bg);color:var(--text);font-family:Lexend,system-ui,sans-serif;padding:16px}
main{width:100%;max-width:400px;background:var(--surface);border:1px solid var(--border);border-radius:12px;padding:28px}
h1{font-size:1.35rem;font-weight:600;margin:0 0 8px}.sub,.note{color:var(--text2);font-size:.9rem;line-height:1.45}
label{display:block;font-size:.85rem;color:var(--text2);margin:14px 0 0}
input:not([type=radio]){display:block;width:100%;margin-top:6px;padding:10px 12px;font:inherit;color:var(--text);background:var(--input);border:1px solid var(--border);border-radius:8px}
fieldset{border:0;padding:0;margin:8px 0 0}.choice{display:flex;gap:10px;align-items:center;color:var(--text);font-size:1rem;padding:10px 12px;border:1px solid var(--border);border-radius:8px;margin-top:8px;cursor:pointer}
button{margin-top:20px;width:100%;padding:11px;font:inherit;font-weight:600;border:0;border-radius:8px;background:var(--accent);color:var(--on-accent);cursor:pointer}button:hover{background:var(--accent-h)}
.row{display:flex;gap:10px}.row button{flex:1}.secondary{background:transparent;color:var(--text);border:1px solid var(--border)}.secondary:hover{background:var(--input)}
.err{color:var(--error);background:var(--error-bg);padding:10px 12px;border-radius:8px;font-size:.9rem}
.brand{font-size:.75rem;color:var(--text2);text-align:center;margin-top:18px}
</style></head><body><main>
""" + body + """
<p class="brand">LucyAPI · Snowcap Systems</p></main></body></html>
""";
        return new HtmlResult(html, statusCode);
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);

    private sealed class HtmlResult(string html, int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext ctx)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-store";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            // No form-action directive: browsers apply it to the post-submit redirect, which must go to the client's callback.
            ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            await ctx.Response.WriteAsync(html, ctx.RequestAborted);
        }
    }
}
