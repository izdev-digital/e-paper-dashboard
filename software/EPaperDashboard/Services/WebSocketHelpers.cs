using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace EPaperDashboard.Services;

public static class WebSocketHelpers
{
    public static async Task<ClientWebSocket> ConnectWebSocket(
        string hostUrl,
        string? accessToken = null,
        string wsPath = "/api/websocket",
        CancellationToken cancellationToken = default)
    {
        var wsUrl = hostUrl.Replace("http://", "ws://").Replace("https://", "wss://");
        var ws = new ClientWebSocket();

        if (!string.IsNullOrEmpty(accessToken))
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
        }

        await ws.ConnectAsync(new Uri(wsUrl + wsPath), cancellationToken);
        return ws;
    }

    public static async Task<ClientWebSocket> ConnectAndAuthenticateAsync(
        string hostUrl,
        string accessToken,
        string wsPath = "/api/websocket",
        CancellationToken cancellationToken = default)
    {
        var ws = await ConnectWebSocket(hostUrl, accessToken, wsPath, cancellationToken);
        await ReceiveMessageAsync(ws, cancellationToken);

        await SendMessageAsync(ws, new { type = "auth", access_token = accessToken }, cancellationToken);

        var authResponse = await ReceiveMessageAsync(ws, cancellationToken);
        var authResult = JsonSerializer.Deserialize<JsonElement>(authResponse);

        if (!authResult.TryGetProperty("type", out var authType) || authType.GetString() != "auth_ok")
        {
            var errorMsg = authResult.TryGetProperty("message", out var msg) ? msg.GetString() : authResponse;
            throw new InvalidOperationException($"Home Assistant authentication failed: {errorMsg}");
        }

        return ws;
    }

    public static async Task SendMessageAsync(ClientWebSocket ws, object message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    public static async Task<string> ReceiveMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[1024 * 16];
        var sb = new StringBuilder();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);
        return sb.ToString();
    }
}
