
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

    public async Task<Result<List<TodoItem>, string>> FetchTodoItems(string dashboardId, string todoEntityId)
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

            var messageId = _messageId++;
            await SendMessageAsync(ws, new
            {
                id = messageId,
                type = "call_service",
                domain = "todo",
                service = "get_items",
                service_data = new
                {
                    entity_id = todoEntityId
                },
                return_response = true
            });

            var response = await ReceiveMessageAsync(ws);
            _logger.LogDebug("HomeAssistant FetchTodoItems raw response: {Response}", response);

            var json = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);

            var items = new List<TodoItem>();

            // The response structure from todo.get_items is:
            // { "success": true, "result": { "response": { "entity_id": { "items": [...] } } } }
            if (json.TryGetProperty("success", out var success) && success.GetBoolean() && 
                json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
            {
                JsonElement itemsArray = default;
                bool foundItems = false;

                // Standard structure: result.response.<entity_id>.items
                if (result.TryGetProperty("response", out var responseObj) && responseObj.ValueKind == JsonValueKind.Object)
                {
                    if (responseObj.TryGetProperty(todoEntityId, out var entityObj) && entityObj.ValueKind == JsonValueKind.Object &&
                        entityObj.TryGetProperty("items", out itemsArray) && itemsArray.ValueKind == JsonValueKind.Array)
                    {
                        foundItems = true;
                        _logger.LogDebug("Found items at result.response.{EntityId}.items", todoEntityId);
                    }
                }

                // Fallback: result.<entity_id>.items (if response is not wrapped)
                if (!foundItems && result.TryGetProperty(todoEntityId, out var entityObj2) && entityObj2.ValueKind == JsonValueKind.Object &&
                    entityObj2.TryGetProperty("items", out itemsArray) && itemsArray.ValueKind == JsonValueKind.Array)
                {
                    foundItems = true;
                    _logger.LogDebug("Found items at result.{EntityId}.items", todoEntityId);
                }

                // Fallback: result.items (direct array)
                if (!foundItems && result.TryGetProperty("items", out itemsArray) && itemsArray.ValueKind == JsonValueKind.Array)
                {
                    foundItems = true;
                    _logger.LogDebug("Found items at result.items");
                }

                if (foundItems)
                {
                    foreach (var item in itemsArray.EnumerateArray())
                    {
                        var summary = item.TryGetProperty("summary", out var s) ? s.GetString() : null;
                        var status = item.TryGetProperty("status", out var st) ? st.GetString() : null;
                        var uid = item.TryGetProperty("uid", out var u) ? u.GetString() : null;
                        items.Add(new TodoItem
                        {
                            Summary = summary ?? string.Empty,
                            Status = status ?? string.Empty,
                            Uid = uid ?? string.Empty
                        });
                    }
                    _logger.LogDebug("Parsed {Count} todo items from entity {EntityId}", items.Count, todoEntityId);
                }
                else
                {
                    _logger.LogWarning("Could not find items array in todo.get_items response for entity {EntityId}. Response was: {Response}", todoEntityId, response);
                }
            }
            else
            {
                _logger.LogWarning("Todo items fetch returned unsuccessful response or missing result property. Response was: {Response}", response);
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            return items;
        }
        catch (WebSocketException)
        {
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            return $"Failed to fetch todo items: {ex.Message}";
        }
    }

    /// <summary>
    /// Fetches upcoming calendar events for a specific calendar entity.
    /// Uses Home Assistant's calendar.get_events service to retrieve events within a specified duration.
    /// Fetches a wider time window (7 days) to provide more event options, displayed count limited by maxEvents.
    /// </summary>
    /// <param name="dashboardId">The dashboard ID for authentication</param>
    /// <param name="calendarEntityId">The calendar entity ID (e.g., calendar.my_calendar)</param>
    /// <param name="durationHours">Hours into the future to fetch events (default: 168 = 7 days)</param>
    /// <returns>List of CalendarEvent objects sorted by start time, or error message</returns>
    public async Task<Result<List<CalendarEvent>, string>> FetchCalendarEvents(string dashboardId, string calendarEntityId, int durationHours = 168)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        if (string.IsNullOrWhiteSpace(calendarEntityId))
        {
            return "Calendar entity ID is required";
        }

        var dashboard = dashboardResult.Value;
        var hostUrl = dashboard.Host!.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, dashboard.AccessToken!);

            var messageId = _messageId++;
            var now = DateTime.UtcNow;
            var endTime = now.AddHours(durationHours);
            
            await SendMessageAsync(ws, new
            {
                id = messageId,
                type = "call_service",
                domain = "calendar",
                service = "get_events",
                service_data = new
                {
                    start_date_time = now.ToString("O"),
                    end_date_time = endTime.ToString("O")
                },
                target = new
                {
                    entity_id = calendarEntityId
                },
                return_response = true
            });

            var response = await ReceiveMessageAsync(ws);
            _logger.LogDebug("HomeAssistant FetchCalendarEvents raw response: {Response}", response);

            var json = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);
            var events = new List<CalendarEvent>();

            // The response structure from calendar.get_events is:
            // { "success": true, "result": { "response": { "calendar.entity_id": { "events": [...] } } } }
            if (json.TryGetProperty("success", out var success) && success.GetBoolean() &&
                json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
            {
                JsonElement eventsArray = default;
                bool foundEvents = false;

                // Try standard structure: result.response.<calendar_entity>.events
                if (result.TryGetProperty("response", out var responseObj) && responseObj.ValueKind == JsonValueKind.Object)
                {
                    if (responseObj.TryGetProperty(calendarEntityId, out var entityObj) && 
                        entityObj.ValueKind == JsonValueKind.Object &&
                        entityObj.TryGetProperty("events", out eventsArray) && 
                        eventsArray.ValueKind == JsonValueKind.Array)
                    {
                        foundEvents = true;
                        _logger.LogDebug("Found events at result.response.{EntityId}.events", calendarEntityId);
                    }
                }

                // Fallback: result.<calendar_entity>.events
                if (!foundEvents && result.TryGetProperty(calendarEntityId, out var entityObj2) && 
                    entityObj2.ValueKind == JsonValueKind.Object &&
                    entityObj2.TryGetProperty("events", out eventsArray) && 
                    eventsArray.ValueKind == JsonValueKind.Array)
                {
                    foundEvents = true;
                    _logger.LogDebug("Found events at result.{EntityId}.events", calendarEntityId);
                }

                // Fallback: result.events (direct array)
                if (!foundEvents && result.TryGetProperty("events", out eventsArray) && 
                    eventsArray.ValueKind == JsonValueKind.Array)
                {
                    foundEvents = true;
                    _logger.LogDebug("Found events at result.events");
                }

                if (foundEvents)
                {
                    foreach (var eventElement in eventsArray.EnumerateArray())
                    {
                        var calendarEvent = ParseCalendarEvent(eventElement);
                        if (calendarEvent != null)
                        {
                            events.Add(calendarEvent);
                        }
                    }

                    // Sort events by start time
                    events = events
                        .OrderBy(e => e.Start)
                        .ToList();

                    _logger.LogDebug("Parsed {Count} calendar events from entity {EntityId} between {Start} and {End}", events.Count, calendarEntityId, now, endTime);
                }
                else
                {
                    _logger.LogWarning("Could not find events array in calendar.get_events response for entity {EntityId}. Response was: {Response}", calendarEntityId, response);
                }
            }
            else
            {
                _logger.LogWarning("Calendar events fetch returned unsuccessful response. Response was: {Response}", response);
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            return events;
        }
        catch (WebSocketException)
        {
            _logger.LogError("Unable to connect to Home Assistant WebSocket for calendar events");
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch calendar events from entity {EntityId}", calendarEntityId);
            return $"Failed to fetch calendar events: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses a single calendar event from the JSON response.
    /// Handles various date/time formats and event properties.
    /// </summary>
    private CalendarEvent? ParseCalendarEvent(JsonElement eventElement)
    {
        try
        {
            if (eventElement.ValueKind != JsonValueKind.Object)
                return null;

            // Extract start time (required)
            string? start = null;
            if (eventElement.TryGetProperty("start", out var startProp))
            {
                start = ExtractDateTimeString(startProp);
            }

            if (string.IsNullOrWhiteSpace(start))
            {
                _logger.LogWarning("Skipping calendar event with missing start time");
                return null;
            }

            // Extract end time
            string? end = null;
            if (eventElement.TryGetProperty("end", out var endProp))
            {
                end = ExtractDateTimeString(endProp);
            }

            // Extract summary/title
            string summary = string.Empty;
            if (eventElement.TryGetProperty("summary", out var summaryProp))
            {
                summary = summaryProp.GetString() ?? string.Empty;
            }

            // Extract description
            string? description = null;
            if (eventElement.TryGetProperty("description", out var descProp))
            {
                description = descProp.GetString();
            }

            // Extract location
            string? location = null;
            if (eventElement.TryGetProperty("location", out var locProp))
            {
                location = locProp.GetString();
            }

            // Extract uid
            string uid = string.Empty;
            if (eventElement.TryGetProperty("uid", out var uidProp))
            {
                uid = uidProp.GetString() ?? Guid.NewGuid().ToString();
            }
            else
            {
                uid = Guid.NewGuid().ToString();
            }

            // Determine if all-day event
            bool allDay = false;
            if (eventElement.TryGetProperty("all_day", out var allDayProp))
            {
                allDay = allDayProp.GetBoolean();
            }

            // Try to infer all-day from start/end format (date-only vs datetime)
            if (!allDay && !string.IsNullOrWhiteSpace(start) && start.Length == 10 && start[4] == '-' && start[7] == '-')
            {
                allDay = true;
            }

            // Extract recurrence rule
            string? recurrenceRule = null;
            if (eventElement.TryGetProperty("rrule", out var rRuleProp))
            {
                recurrenceRule = rRuleProp.GetString();
            }

            return new CalendarEvent
            {
                Uid = uid,
                Summary = summary,
                Description = description,
                Location = location,
                Start = start,
                End = end,
                AllDay = allDay,
                RecurrenceRule = recurrenceRule
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse calendar event element");
            return null;
        }
    }

    /// <summary>
    /// Extracts a date/time string from a JSON element.
    /// Handles both string and object representations of dates.
    /// </summary>
    private string? ExtractDateTimeString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => 
                element.TryGetProperty("__type", out var typeElement) && typeElement.GetString() == "ISO8601_STR" &&
                element.TryGetProperty("isoformat", out var isoProp) ? isoProp.GetString() : null,
            _ => null
        };
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

public record TodoItem
{
    public string Summary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Uid { get; init; } = string.Empty;
}

public record CalendarEvent
{
    /// <summary>
    /// Event unique identifier
    /// </summary>
    public string Uid { get; init; } = string.Empty;

    /// <summary>
    /// Event title/summary
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Event description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Event location
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Event start time (ISO 8601 format)
    /// </summary>
    public string Start { get; init; } = string.Empty;

    /// <summary>
    /// Event end time (ISO 8601 format)
    /// </summary>
    public string? End { get; init; }

    /// <summary>
    /// Whether this is an all-day event
    /// </summary>
    public bool AllDay { get; init; }

    /// <summary>
    /// Recurrence rule if the event repeats
    /// </summary>
    public string? RecurrenceRule { get; init; }
}
