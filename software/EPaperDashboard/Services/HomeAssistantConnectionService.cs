using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services;

/// <summary>
/// Provides shared Home Assistant connection infrastructure: dashboard validation,
/// host/token resolution, WebSocket connectivity, and JSON helpers.
/// Used by HA provider implementations and HomeAssistantService.
/// </summary>
public class HomeAssistantConnectionService(
    DashboardService dashboardService,
    IDeploymentStrategy deploymentStrategy)
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly IDeploymentStrategy _deploymentStrategy = deploymentStrategy;
    private int _messageId = 2;

    public string WebSocketPath => _deploymentStrategy.WebSocketPath;

    public (string host, string token) GetHostAndToken(Dashboard dashboard)
    {
        return _deploymentStrategy.GetHomeAssistantConnection(dashboard);
    }

    public Result<Dashboard, string> ValidateAndGetDashboard(string dashboardId)
    {
        if (string.IsNullOrWhiteSpace(dashboardId))
        {
            return "Dashboard ID is required";
        }

        if (!DashboardId.TryParse(dashboardId, out var dashboardIdTyped))
        {
            return "Invalid dashboard ID format";
        }

        var dashboardMaybe = _dashboardService.GetDashboardById(dashboardIdTyped);
        if (dashboardMaybe.HasNoValue)
        {
            return "Dashboard not found";
        }

        var dashboard = dashboardMaybe.Value;

        if (_deploymentStrategy.IsAutoConnected)
        {
            return dashboard;
        }

        return Result.Success<Dashboard, string>(dashboard)
            .Ensure(d => !string.IsNullOrWhiteSpace(d.Host), "Dashboard host is not configured")
            .Ensure(d => !string.IsNullOrWhiteSpace(d.AccessToken), "Dashboard access token is not set. Please authenticate with Home Assistant first.");
    }

    /// <summary>
    /// Returns a connected and authenticated WebSocket to Home Assistant for the given dashboard.
    /// Caller is responsible for disposing the returned WebSocket.
    /// </summary>
    public async Task<ClientWebSocket> ConnectAsync(string dashboardId)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            throw new InvalidOperationException(dashboardResult.Error);
        }

        var dashboard = dashboardResult.Value;
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        return await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token, WebSocketPath);
    }

    /// <summary>
    /// Returns host URL and bearer token for the given dashboard, after validation.
    /// </summary>
    public Result<(string hostUrl, string token), string> GetConnectionInfo(string dashboardId)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        var dashboard = dashboardResult.Value;
        var (hostUrl, token) = GetHostAndToken(dashboard);
        return (hostUrl.TrimEnd('/'), token);
    }

    public int NextMessageId() => _messageId++;

    public static async Task SendMessageAsync(ClientWebSocket ws, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static async Task<string> ReceiveMessageAsync(ClientWebSocket ws)
    {
        return await ReceiveMessageAsync(ws, CancellationToken.None);
    }

    public static async Task<string> ReceiveMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
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

    public static object? ExtractJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDouble(out var d) ? d : null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.ToString()) ?? new Dictionary<string, object?>(),
            JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(element.ToString()) ?? new List<object?>(),
            _ => null
        };
    }
}
