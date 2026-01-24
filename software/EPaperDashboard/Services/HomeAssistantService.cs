using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using LiteDB;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services;

public class HomeAssistantService(
    ILogger<HomeAssistantService> logger,
    DashboardService dashboardService)
{
    private readonly ILogger<HomeAssistantService> _logger = logger;
    private readonly DashboardService _dashboardService = dashboardService;
    private int _messageId = 2;

    public async Task<Result<List<HassUrlInfo>, string>> FetchDashboards(string dashboardId)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        var dashboard = dashboardResult.Value;
        var hostUrl = dashboard.Host!.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, dashboard.AccessToken!);
            var results = await FetchAllDashboardViews(ws, hostUrl);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            return results;
        }
        catch (WebSocketException)
        {
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            return $"Failed to fetch dashboards: {ex.Message}";
        }
    }

    public async Task<Result<List<HassEntity>, string>> FetchEntities(string dashboardId)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        var dashboard = dashboardResult.Value;
        var hostUrl = dashboard.Host!.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, dashboard.AccessToken!);
            
            await SendMessageAsync(ws, new
            {
                id = 1,
                type = "get_states"
            });

            var statesResponse = await ReceiveMessageAsync(ws);
            var statesResult = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(statesResponse);

            var entities = new List<HassEntity>();
            
            if (statesResult.TryGetProperty("success", out var success) && success.GetBoolean() &&
                statesResult.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in result.EnumerateArray())
                {
                    var entityId = entity.TryGetProperty("entity_id", out var eid) ? eid.GetString() : null;
                    var friendlyName = string.Empty;
                    
                    if (entity.TryGetProperty("attributes", out var attrs) &&
                        attrs.TryGetProperty("friendly_name", out var fname))
                    {
                        friendlyName = fname.GetString() ?? string.Empty;
                    }

                    if (!string.IsNullOrEmpty(entityId))
                    {
                        entities.Add(new HassEntity
                        {
                            EntityId = entityId,
                            FriendlyName = friendlyName
                        });
                    }
                }
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            return entities;
        }
        catch (WebSocketException)
        {
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            return $"Failed to fetch entities: {ex.Message}";
        }
    }

    public async Task<Result<List<HassEntityState>, string>> FetchEntityStates(string dashboardId, IEnumerable<string> entityIds)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        var requestedIds = new HashSet<string>(entityIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        if (requestedIds.Count == 0)
        {
            return Result.Success<List<HassEntityState>, string>(new List<HassEntityState>());
        }

        var dashboard = dashboardResult.Value;
        var hostUrl = dashboard.Host!.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, dashboard.AccessToken!);

            await SendMessageAsync(ws, new
            {
                id = 1,
                type = "get_states"
            });

            var statesResponse = await ReceiveMessageAsync(ws);
            var statesResult = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(statesResponse);

            var entityStates = new List<HassEntityState>();

            if (statesResult.TryGetProperty("success", out var success) && success.GetBoolean() &&
                statesResult.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in result.EnumerateArray())
                {
                    var entityId = entity.TryGetProperty("entity_id", out var eid) ? eid.GetString() : null;
                    if (string.IsNullOrWhiteSpace(entityId) || !requestedIds.Contains(entityId))
                    {
                        continue;
                    }

                    var state = entity.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : string.Empty;
                    var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    if (entity.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var attr in attrs.EnumerateObject())
                        {
                            attributes[attr.Name] = ExtractJsonValue(attr.Value);
                        }
                    }

                    entityStates.Add(new HassEntityState
                    {
                        EntityId = entityId,
                        State = state ?? string.Empty,
                        Attributes = attributes
                    });
                }
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            return entityStates;
        }
        catch (WebSocketException)
        {
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            return $"Failed to fetch entity states: {ex.Message}";
        }
    }

    private Result<Models.Dashboard, string> ValidateAndGetDashboard(string dashboardId)
    {
        if (string.IsNullOrWhiteSpace(dashboardId))
        {
            return "Dashboard ID is required";
        }

        ObjectId objectId;
        try
        {
            objectId = new ObjectId(dashboardId);
        }
        catch
        {
            return "Invalid dashboard ID format";
        }

        var dashboardMaybe = _dashboardService.GetDashboardById(objectId);
        if (dashboardMaybe.HasNoValue)
        {
            return "Dashboard not found";
        }

        var dashboard = dashboardMaybe.Value;

        return Result.Success<Models.Dashboard, string>(dashboard)
            .Ensure(d => !string.IsNullOrWhiteSpace(d.Host), "Dashboard host is not configured")
            .Ensure(d => !string.IsNullOrWhiteSpace(d.AccessToken), "Dashboard access token is not set. Please authenticate with Home Assistant first.");
    }

    

    private async Task<List<HassUrlInfo>> FetchAllDashboardViews(ClientWebSocket ws, string hostUrl)
    {
        var results = new List<HassUrlInfo>();

        await SendMessageAsync(ws, new
        {
            id = 1,
            type = "lovelace/dashboards/list"
        });

        var dashboardsResponse = await ReceiveMessageAsync(ws);
        var dashboardsResult = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(dashboardsResponse);

        var fetchedDashboards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var isSuccess = dashboardsResult.TryGetProperty("success", out var success) && success.GetBoolean();
        if (!isSuccess || !dashboardsResult.TryGetProperty("result", out var dashboardsArray))
        {
            await GetDashboardViews(ws, hostUrl, "lovelace", "Home", results);
            return results;
        }

        foreach (var hassDb in dashboardsArray.EnumerateArray())
        {
            var urlPath = hassDb.TryGetProperty("url_path", out var up) ? up.GetString() : null;
            var title = hassDb.TryGetProperty("title", out var t) ? t.GetString() : null;

            if (string.IsNullOrWhiteSpace(urlPath) || string.IsNullOrWhiteSpace(title))
                continue;

            await GetDashboardViews(ws, hostUrl, urlPath, title, results);
            fetchedDashboards.Add(urlPath);
        }

        if (!fetchedDashboards.Contains("lovelace"))
        {
            await GetDashboardViews(ws, hostUrl, "lovelace", "Home", results);
        }

        return results;
    }

    private async Task<string> ReceiveMessageAsync(ClientWebSocket ws)
    {
        var buffer = new byte[1024 * 16];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private async Task SendMessageAsync(ClientWebSocket ws, object message)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static object? ExtractJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.TryGetDouble(out var d) ? d : null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(element.ToString()) ?? new Dictionary<string, object?>(),
            JsonValueKind.Array => System.Text.Json.JsonSerializer.Deserialize<List<object?>>(element.ToString()) ?? new List<object?>(),
            _ => null
        };
    }

    private async Task GetDashboardViews(ClientWebSocket ws, string hostUrl, string urlPath, string dashboardTitle, List<HassUrlInfo> results)
    {
        try
        {
            var messageId = _messageId++;

            await SendMessageAsync(ws, new
            {
                id = messageId,
                type = "lovelace/config",
                url_path = urlPath == "lovelace" ? (string?)null : urlPath
            });

            var configResponse = await ReceiveMessageAsync(ws);
            var configResult = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(configResponse);

            var isSuccess = configResult.TryGetProperty("success", out var success) && success.GetBoolean();
            if (!isSuccess)
            {
                results.AddRange(CreateDefaultDashboardInfo(hostUrl, urlPath, dashboardTitle));
                return;
            }

            if (!configResult.TryGetProperty("result", out var config) ||
                !config.TryGetProperty("views", out var views) ||
                views.ValueKind != JsonValueKind.Array)
            {
                results.AddRange(CreateDefaultDashboardInfo(hostUrl, urlPath, dashboardTitle));
                return;
            }

            var viewsArray = views.EnumerateArray().ToList();
            if (viewsArray.Count == 0)
            {
                results.AddRange(CreateDefaultDashboardInfo(hostUrl, urlPath, dashboardTitle));
                return;
            }

            results.AddRange(ConvertViewsToResults(viewsArray, hostUrl, urlPath, dashboardTitle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching views for dashboard {Dashboard}", urlPath);
            results.AddRange(CreateDefaultDashboardInfo(hostUrl, urlPath, dashboardTitle));
        }
    }

    public async Task<Result<bool, string>> SendNotification(Models.Dashboard dashboard, string message, string title = "EPaper Dashboard")
    {
        var validationResult = ValidateAndGetDashboard(dashboard.Id.ToString());
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        var hostUrl = dashboard.Host!.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, dashboard.AccessToken!);
            
            var messageId = _messageId++;
            await SendMessageAsync(ws, new
            {
                id = messageId,
                type = "call_service",
                domain = "persistent_notification",
                service = "create",
                service_data = new
                {
                    title = title,
                    message = message,
                    notification_id = $"epaper_dashboard_{dashboard.Id}"
                }
            });

            var response = await ReceiveMessageAsync(ws);
            var result = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            var isSuccess = result.TryGetProperty("success", out var success) && success.GetBoolean();
            if (!isSuccess)
            {
                var errorMsg = result.TryGetProperty("error", out var error) 
                    ? error.GetProperty("message").GetString() 
                    : "Unknown error";
                return $"Failed to send notification: {errorMsg}";
            }

            _logger.LogInformation("Notification sent to Home Assistant for dashboard {DashboardName}", dashboard.Name);
            return true;
        }
        catch (WebSocketException ex)
        {
            _logger.LogError(ex, "Unable to connect to Home Assistant for dashboard {DashboardName}", dashboard.Name);
            return "Unable to connect to Home Assistant WebSocket";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification for dashboard {DashboardName}", dashboard.Name);
            return $"Failed to send notification: {ex.Message}";
        }
    }

    private static IEnumerable<HassUrlInfo> CreateDefaultDashboardInfo(string hostUrl, string urlPath, string dashboardTitle)
    {
        yield return new HassUrlInfo
        {
            Url = $"{hostUrl}/{urlPath}",
            Title = dashboardTitle,
            Id = urlPath
        };
    }

    private static IEnumerable<HassUrlInfo> ConvertViewsToResults(List<JsonElement> viewsArray, string hostUrl, string urlPath, string dashboardTitle)
    {
        for (int i = 0; i < viewsArray.Count; i++)
        {
            var view = viewsArray[i];
            var viewPath = view.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;
            var viewTitle = view.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;

            var viewUrl = string.IsNullOrWhiteSpace(viewPath)
                ? $"{hostUrl}/{urlPath}/{i}"
                : $"{hostUrl}/{urlPath}/{viewPath}";

            var displayTitle = !string.IsNullOrWhiteSpace(viewTitle)
                ? $"{dashboardTitle} - {viewTitle}"
                : $"{dashboardTitle} - View {i + 1}";

            var viewId = string.IsNullOrWhiteSpace(viewPath)
                ? $"{urlPath}/{i}"
                : $"{urlPath}/{viewPath}";

            yield return new HassUrlInfo
            {
                Url = viewUrl,
                Title = displayTitle,
                Id = viewId
            };
        }
    }
}

public record HassUrlInfo
{
    public string Url { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
}

public record HassEntity
{
    public string EntityId { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
}

public record HassEntityState
{
    public string EntityId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public Dictionary<string, object?> Attributes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
