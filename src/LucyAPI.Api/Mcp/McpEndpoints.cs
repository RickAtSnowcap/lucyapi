using System.Buffers;
using System.Text.Json;

namespace LucyAPI.Api.Mcp;

public static class McpEndpoints
{
    private const string ProtocolVersion = "2025-03-26";
    private const string ServerName = "lucyapi-mcp";
    private const string ServerVersion = "2.0.0";

    public static void MapMcpEndpoints(this WebApplication app)
    {
        app.MapPost("/mcp/", HandlePost);
        app.MapDelete("/mcp/", () => Results.Accepted());
    }

    private static async Task HandlePost(HttpContext ctx, McpToolDispatcher dispatcher)
    {
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
        }
        catch
        {
            await WriteJsonRpcError(ctx, default, -32700, "Parse error");
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("method", out var methodProp))
            {
                await WriteJsonRpcError(ctx, default, -32600, "Invalid request: missing method");
                return;
            }

            var method = methodProp.GetString() ?? "";
            var hasId = root.TryGetProperty("id", out var idProp);

            // Notifications (no id) — acknowledge with 202
            if (!hasId)
            {
                ctx.Response.StatusCode = 202;
                return;
            }

            switch (method)
            {
                case "initialize":
                    await HandleInitialize(ctx, idProp);
                    break;

                case "tools/list":
                    await HandleToolsList(ctx, idProp, dispatcher);
                    break;

                case "tools/call":
                    await HandleToolCall(ctx, idProp, root, dispatcher);
                    break;

                default:
                    await WriteJsonRpcError(ctx, idProp, -32601, "Method not found: " + method);
                    break;
            }
        }
    }

    private static async Task HandleInitialize(HttpContext ctx, JsonElement id)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        ctx.Response.Headers["Mcp-Session-Id"] = sessionId;

        var buffer = new ArrayBufferWriter<byte>();
        using var w = new Utf8JsonWriter(buffer);
        w.WriteStartObject();
        w.WriteString("jsonrpc", "2.0");
        WriteId(w, id);
        w.WriteStartObject("result");
        w.WriteString("protocolVersion", ProtocolVersion);
        w.WriteStartObject("capabilities");
        w.WriteStartObject("tools");
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteStartObject("serverInfo");
        w.WriteString("name", ServerName);
        w.WriteString("version", ServerVersion);
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndObject();
        w.Flush();

        ctx.Response.ContentType = "application/json";
        await ctx.Response.Body.WriteAsync(buffer.WrittenMemory, ctx.RequestAborted);
    }

    private static async Task HandleToolsList(HttpContext ctx, JsonElement id, McpToolDispatcher dispatcher)
    {
        var toolsJson = dispatcher.GetToolListJson();

        var buffer = new ArrayBufferWriter<byte>();
        using var w = new Utf8JsonWriter(buffer);
        w.WriteStartObject();
        w.WriteString("jsonrpc", "2.0");
        WriteId(w, id);
        w.WritePropertyName("result");
        w.WriteRawValue(toolsJson);
        w.WriteEndObject();
        w.Flush();

        ctx.Response.ContentType = "application/json";
        await ctx.Response.Body.WriteAsync(buffer.WrittenMemory, ctx.RequestAborted);
    }

    private static async Task HandleToolCall(HttpContext ctx, JsonElement id, JsonElement root, McpToolDispatcher dispatcher)
    {
        if (!root.TryGetProperty("params", out var paramsProp))
        {
            await WriteJsonRpcError(ctx, id, -32602, "Missing params");
            return;
        }

        var toolName = "";
        if (paramsProp.TryGetProperty("name", out var nameProp))
            toolName = nameProp.GetString() ?? "";

        JsonElement arguments = default;
        paramsProp.TryGetProperty("arguments", out arguments);

        string resultText;
        try
        {
            resultText = await dispatcher.DispatchAsync(toolName, arguments, ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            resultText = "{\"error\":\"" + EscapeJsonString(ex.Message) + "\"}";
        }

        var buffer = new ArrayBufferWriter<byte>();
        using var w = new Utf8JsonWriter(buffer);
        w.WriteStartObject();
        w.WriteString("jsonrpc", "2.0");
        WriteId(w, id);
        w.WriteStartObject("result");
        w.WriteStartArray("content");
        w.WriteStartObject();
        w.WriteString("type", "text");
        w.WriteString("text", resultText);
        w.WriteEndObject();
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
        w.Flush();

        ctx.Response.ContentType = "application/json";
        await ctx.Response.Body.WriteAsync(buffer.WrittenMemory, ctx.RequestAborted);
    }

    private static async Task WriteJsonRpcError(HttpContext ctx, JsonElement id, int code, string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var w = new Utf8JsonWriter(buffer);
        w.WriteStartObject();
        w.WriteString("jsonrpc", "2.0");
        if (id.ValueKind != JsonValueKind.Undefined)
            WriteId(w, id);
        else
            w.WriteNull("id");
        w.WriteStartObject("error");
        w.WriteNumber("code", code);
        w.WriteString("message", message);
        w.WriteEndObject();
        w.WriteEndObject();
        w.Flush();

        ctx.Response.ContentType = "application/json";
        await ctx.Response.Body.WriteAsync(buffer.WrittenMemory, ctx.RequestAborted);
    }

    private static void WriteId(Utf8JsonWriter w, JsonElement id)
    {
        w.WritePropertyName("id");
        id.WriteTo(w);
    }

    internal static string EscapeJsonString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}
