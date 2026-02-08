
using System.Text.Json;
using System.Net.WebSockets;
using System.Text;
using LiteDB;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services;

public class HomeAssistantService(
    ILogger<HomeAssistantService> logger,
    DashboardService dashboardService,
    HomeAssistantEnvironmentService haEnvironment)
{
    private readonly ILogger<HomeAssistantService> _logger = logger;
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly HomeAssistantEnvironmentService _haEnvironment = haEnvironment;
    private int _messageId = 2;

    private (string host, string token) GetHostAndToken(Dashboard dashboard)
    {
        if (_haEnvironment.IsValidHomeAssistantEnvironment())
        {
            return ("http://supervisor/core", _haEnvironment.SupervisorToken!);
        }
        return (dashboard.Host!, dashboard.AccessToken!);
    }

    public async Task<Result<List<HassUrlInfo>, string>> FetchDashboards(string dashboardId)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        var dashboard = dashboardResult.Value;
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);
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
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);
            
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
                    string? deviceClass = null;
                    string? unitOfMeasurement = null;
                    string? icon = null;
                    string? state = null;
                    int? supportedFeatures = null;
                    
                    if (entity.TryGetProperty("state", out var stateProp))
                    {
                        state = stateProp.GetString();
                    }

                    if (entity.TryGetProperty("attributes", out var attrs))
                    {
                        if (attrs.TryGetProperty("friendly_name", out var fname))
                        {
                            friendlyName = fname.GetString() ?? string.Empty;
                        }

                        if (attrs.TryGetProperty("device_class", out var deviceClassProp))
                        {
                            deviceClass = deviceClassProp.GetString();
                        }

                        if (attrs.TryGetProperty("unit_of_measurement", out var unitProp))
                        {
                            unitOfMeasurement = unitProp.GetString();
                        }

                        if (attrs.TryGetProperty("icon", out var iconProp))
                        {
                            icon = iconProp.GetString();
                        }

                        if (attrs.TryGetProperty("supported_features", out var supportedFeaturesProp))
                        {
                            if (supportedFeaturesProp.ValueKind == JsonValueKind.Number && supportedFeaturesProp.TryGetInt32(out var supported))
                            {
                                supportedFeatures = supported;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(entityId))
                    {
                        var domain = entityId.Split('.', 2)[0];
                        entities.Add(new HassEntity
                        {
                            EntityId = entityId,
                            FriendlyName = friendlyName,
                            Domain = domain,
                            DeviceClass = deviceClass,
                            UnitOfMeasurement = unitOfMeasurement,
                            Icon = icon,
                            State = state,
                            SupportedFeatures = supportedFeatures
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
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);

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
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);

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
    /// Fetches weather forecast data for a weather entity.
    /// Uses Home Assistant's weather.get_forecasts service to retrieve forecast data.
    /// </summary>
    /// <param name="dashboardId">The dashboard ID for authentication</param>
    /// <param name="weatherEntityId">The weather entity ID (e.g., weather.openmeteo_home)</param>
    /// <param name="forecastType">The type of forecast: 'hourly', 'daily', or 'twice_daily' (default: 'daily')</param>
    /// <returns>Dictionary with forecast array, or error message</returns>
    public async Task<Result<Dictionary<string, object?>, string>> FetchWeatherForecast(string dashboardId, string weatherEntityId, string forecastType = "daily")
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        if (string.IsNullOrWhiteSpace(weatherEntityId))
        {
            return "Weather entity ID is required";
        }

        var dashboard = dashboardResult.Value;
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);

            var messageId = _messageId++;
            
            await SendMessageAsync(ws, new
            {
                id = messageId,
                type = "call_service",
                domain = "weather",
                service = "get_forecasts",
                service_data = new
                {
                    type = forecastType
                },
                target = new
                {
                    entity_id = weatherEntityId
                },
                return_response = true
            });

            var response = await ReceiveMessageAsync(ws);
            _logger.LogDebug("HomeAssistant FetchWeatherForecast raw response: {Response}", response);

            var json = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);
            var forecastData = new Dictionary<string, object?>();

            // The response structure from weather.get_forecasts is:
            // { "success": true, "result": { "response": { "weather.entity_id": { "forecast": [...] } } } }
            if (json.TryGetProperty("success", out var success) && success.GetBoolean() &&
                json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
            {
                JsonElement forecastArray = default;
                bool foundForecast = false;

                // Try standard structure: result.response.<weather_entity>.forecast
                if (result.TryGetProperty("response", out var responseObj) && responseObj.ValueKind == JsonValueKind.Object)
                {
                    if (responseObj.TryGetProperty(weatherEntityId, out var entityObj) && 
                        entityObj.ValueKind == JsonValueKind.Object &&
                        entityObj.TryGetProperty("forecast", out forecastArray) && 
                        forecastArray.ValueKind == JsonValueKind.Array)
                    {
                        foundForecast = true;
                        _logger.LogDebug("Found forecast at result.response.{EntityId}.forecast", weatherEntityId);
                    }
                }

                // Fallback: result.<weather_entity>.forecast
                if (!foundForecast && result.TryGetProperty(weatherEntityId, out var entityObj2) && 
                    entityObj2.ValueKind == JsonValueKind.Object &&
                    entityObj2.TryGetProperty("forecast", out forecastArray) && 
                    forecastArray.ValueKind == JsonValueKind.Array)
                {
                    foundForecast = true;
                    _logger.LogDebug("Found forecast at result.{EntityId}.forecast", weatherEntityId);
                }

                // Fallback: result.forecast (direct array)
                if (!foundForecast && result.TryGetProperty("forecast", out forecastArray) && 
                    forecastArray.ValueKind == JsonValueKind.Array)
                {
                    foundForecast = true;
                    _logger.LogDebug("Found forecast at result.forecast");
                }

                if (foundForecast)
                {
                    // Convert JsonElement forecast items to list of dictionaries
                    var forecastList = new List<object?>();
                    foreach (var item in forecastArray.EnumerateArray())
                    {
                        var forecastItem = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in item.EnumerateObject())
                        {
                            forecastItem[prop.Name] = ExtractJsonValue(prop.Value);
                        }
                        forecastList.Add(forecastItem);
                    }
                    forecastData["forecast"] = forecastList;
                    _logger.LogDebug("Parsed {Count} forecast items from entity {EntityId}", forecastList.Count, weatherEntityId);
                    
                    // Log first few items for debugging
                    if (forecastList.Count > 0)
                    {
                        var firstItem = forecastList[0] as Dictionary<string, object?>;
                        _logger.LogDebug("First forecast item datetime: {DateTime}", firstItem?["datetime"] ?? "NOT FOUND");
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find forecast array in weather.get_forecasts response for entity {EntityId}. Response was: {Response}", weatherEntityId, response);
                }
            }
            else
            {
                _logger.LogWarning("Weather forecast fetch returned unsuccessful response. Response was: {Response}", response);
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            return forecastData;
        }
        catch (WebSocketException)
        {
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            return $"Failed to fetch weather forecast: {ex.Message}";
        }
    }

    /// <summary>
    /// Fetches RSS feed entries from a Home Assistant feedreader event entity.
    /// The feedreader component creates event entities (event.feed_name) that store
    /// the latest feed entry data in the event attributes (title, link, description, content).
    /// </summary>
    /// <param name="dashboardId">The dashboard ID for authentication</param>
    /// <param name="feedEntityId">The feedreader event entity ID (e.g., event.my_feed_latest_feed)</param>
    /// <returns>List of RssFeedEntry objects, or error message</returns>
    public async Task<Result<List<RssFeedEntry>, string>> FetchRssFeedEntries(string dashboardId, string feedEntityId)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        if (string.IsNullOrWhiteSpace(feedEntityId))
        {
            return "Feed entity ID is required";
        }

        var dashboard = dashboardResult.Value;
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);

            var messageId = _messageId++;
            await SendMessageAsync(ws, new
            {
                id = messageId,
                type = "get_states"
            });

            var response = await ReceiveMessageAsync(ws);
            _logger.LogDebug("HomeAssistant FetchRssFeedEntries raw response: {Response}", response);

            var json = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(response);
            var entries = new List<RssFeedEntry>();

            // Find the specific event entity in the states response
            if (json.TryGetProperty("success", out var success) && success.GetBoolean() &&
                json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in result.EnumerateArray())
                {
                    var entityId = entity.TryGetProperty("entity_id", out var eid) ? eid.GetString() : null;
                    
                    if (entityId == feedEntityId)
                    {
                        // Found the event entity, extract the latest feed entry from attributes
                        if (entity.TryGetProperty("attributes", out var attributes) &&
                            attributes.ValueKind == JsonValueKind.Object)
                        {
                            // The event entity stores the latest entry data directly in attributes
                            var entry = ParseRssFeedEntry(attributes);
                            if (entry != null)
                            {
                                entries.Add(entry);
                            }
                            
                            _logger.LogDebug("Parsed RSS feed entry from event entity {EntityId}", feedEntityId);
                        }
                        else
                        {
                            _logger.LogWarning("Feed event entity {EntityId} found but has no attributes", feedEntityId);
                        }
                        break;
                    }
                }

                if (entries.Count == 0 && !result.EnumerateArray().Any(e => e.TryGetProperty("entity_id", out var eid) && eid.GetString() == feedEntityId))
                {
                    _logger.LogWarning("Feed event entity {EntityId} not found in states. Available event entities: {EventEntities}", 
                        feedEntityId,
                        string.Join(", ", result.EnumerateArray()
                            .Where(e => e.TryGetProperty("entity_id", out var eid) && eid.GetString()?.StartsWith("event.") == true)
                            .Select(e => e.TryGetProperty("entity_id", out var eid) ? eid.GetString() : "unknown")
                        ));
                }
            }
            else
            {
                _logger.LogWarning("RSS feed entries fetch returned unsuccessful response or missing result property");
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            return entries;
        }
        catch (WebSocketException)
        {
            _logger.LogError("Unable to connect to Home Assistant WebSocket for RSS feed entries");
            return "Unable to connect to Home Assistant WebSocket. Please check the Host URL and ensure it's accessible.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch RSS feed entries from entity {EntityId}", feedEntityId);
            return $"Failed to fetch RSS feed entries: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses a single RSS feed entry from event entity attributes.
    /// Home Assistant's feedreader stores the latest entry data with keys: title, link, description, content
    /// </summary>
    private RssFeedEntry? ParseRssFeedEntry(JsonElement attributesElement)
    {
        try
        {
            if (attributesElement.ValueKind != JsonValueKind.Object)
                return null;

            // Event entity attributes store feed data directly
            var title = attributesElement.TryGetProperty("title", out var titleProp) 
                ? titleProp.GetString() ?? string.Empty 
                : string.Empty;

            var link = attributesElement.TryGetProperty("link", out var linkProp) 
                ? linkProp.GetString() ?? string.Empty 
                : string.Empty;

            // Try multiple property names for description
            string? description = null;
            if (attributesElement.TryGetProperty("description", out var descProp))
            {
                description = descProp.GetString();
            }
            else if (attributesElement.TryGetProperty("summary", out var summaryProp))
            {
                description = summaryProp.GetString();
            }
            else if (attributesElement.TryGetProperty("content", out var contentProp))
            {
                description = contentProp.GetString();
            }

            // Require at least a title or link
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(link))
            {
                _logger.LogWarning("Skipping RSS entry with missing title and link");
                return null;
            }

            return new RssFeedEntry
            {
                Title = title,
                Link = link,
                Published = null, // Event entity doesn't store publication date in attributes
                Summary = description
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse RSS feed entry from event attributes");
            return null;
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
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);

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

    /// <summary>
    /// Fetches historical data for one or more entities using Home Assistant's REST API.
    /// Returns data points with timestamps within the specified time period.
    /// The API endpoint is: /api/history/period/{time_period}?filter_entity_ids={entity_ids}
    /// </summary>
    /// <param name="dashboardId">Dashboard ID for authentication</param>
    /// <param name="entityIds">List of entity IDs to fetch history for</param>
    /// <param name="hours">Number of hours back to fetch (1, 6, 24, 168, 720)</param>
    /// <returns>Dictionary mapping entity ID to list of historical states</returns>
    public async Task<Result<Dictionary<string, List<HistoryState>>, string>> FetchEntityHistory(string dashboardId, IEnumerable<string> entityIds, int hours = 24)
    {
        var dashboardResult = ValidateAndGetDashboard(dashboardId);
        if (dashboardResult.IsFailure)
        {
            return dashboardResult.Error;
        }

        var requestedIds = new HashSet<string>(entityIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        if (requestedIds.Count == 0)
        {
            return Result.Success<Dictionary<string, List<HistoryState>>, string>(new Dictionary<string, List<HistoryState>>());
        }

        var dashboard = dashboardResult.Value;
        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using (var httpClient = new HttpClient())
            {
                // Set authorization header
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {dashboard.AccessToken}");

                // Build query parameters - Home Assistant expects filter_entity_id (singular) repeated for each entity
                var entityIdParams = string.Join("&", requestedIds.Select(id => $"filter_entity_id={Uri.EscapeDataString(id)}"));
                var startTime = DateTime.UtcNow.AddHours(-hours).ToString("O");
                var endTime = DateTime.UtcNow.ToString("O");

                // Home Assistant history API endpoint
                var url = $"{hostUrl}/api/history/period/{startTime}?{entityIdParams}&end_time={endTime}";

                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("History API call failed with status {Status}: {Error}", response.StatusCode, errorContent);
                    return $"Failed to fetch history: {response.StatusCode}";
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("HomeAssistant FetchEntityHistory raw response: {Response}", content);

                var historyData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(content);
                var result = new Dictionary<string, List<HistoryState>>();

                // Parse the response which is an array of arrays (one per entity)
                // Format: [[{entity_id, state, attributes, last_changed}, ...], ...]
                if (historyData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entityHistory in historyData.EnumerateArray())
                    {
                        if (entityHistory.ValueKind != JsonValueKind.Array || entityHistory.GetArrayLength() == 0)
                            continue;

                        // Get the first item to extract entity_id
                        var firstState = entityHistory[0];
                        if (!firstState.TryGetProperty("entity_id", out var entityIdProp))
                            continue;

                        var entityId = entityIdProp.GetString();
                        if (string.IsNullOrWhiteSpace(entityId))
                            continue;

                        var states = new List<HistoryState>();

                        // Process each state change for this entity
                        foreach (var stateItem in entityHistory.EnumerateArray())
                        {
                            var historyState = ParseHistoryState(stateItem);
                            if (historyState != null)
                            {
                                states.Add(historyState);
                            }
                        }

                        result[entityId] = states;
                    }
                }

                return Result.Success<Dictionary<string, List<HistoryState>>, string>(result);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching entity history");
            return $"Failed to fetch entity history: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch entity history");
            return $"Failed to fetch entity history: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses a single history state entry from Home Assistant's history API response.
    /// </summary>
    private HistoryState? ParseHistoryState(JsonElement element)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            var state = element.TryGetProperty("state", out var stateProp) 
                ? stateProp.GetString() ?? string.Empty 
                : string.Empty;

            var lastChangedStr = element.TryGetProperty("last_changed", out var lastChangedProp)
                ? lastChangedProp.GetString()
                : null;

            if (!DateTime.TryParse(lastChangedStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastChanged))
            {
                lastChanged = DateTime.UtcNow;
            }

            var attributes = new Dictionary<string, JsonElement>();
            if (element.TryGetProperty("attributes", out var attrsProp) && attrsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var attr in attrsProp.EnumerateObject())
                {
                    attributes[attr.Name] = attr.Value;
                }
            }

            // Try to parse state as numeric value
            double numericValue = 0;
            if (!double.TryParse(state, System.Globalization.CultureInfo.InvariantCulture, out numericValue))
            {
                // If state is not numeric, try to extract from common attribute fields
                // This handles entities like climate (current_temperature), weather (temperature), 
                // light (brightness), cover (current_position), etc.
                if (element.TryGetProperty("entity_id", out var entityIdProp))
                {
                    var entityId = entityIdProp.GetString() ?? string.Empty;
                    numericValue = ExtractNumericFromAttributes(entityId, attributes, state);
                }
            }

            return new HistoryState
            {
                State = state,
                NumericValue = numericValue,
                LastChanged = lastChanged,
                Attributes = attributes
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse history state element");
            return null;
        }
    }

    /// <summary>
    /// Extracts numeric value from entity attributes based on entity type.
    /// </summary>
    private double ExtractNumericFromAttributes(string entityId, Dictionary<string, JsonElement> attributes, string state)
    {
        // Try common numeric attributes based on entity domain
        var domain = entityId.Split('.')[0];
        
        string[] candidateAttributes = domain switch
        {
            "climate" => new[] { "current_temperature", "temperature", "current_humidity", "humidity" },
            "weather" => new[] { "temperature", "humidity", "pressure", "wind_speed" },
            "light" => new[] { "brightness", "color_temp" },
            "cover" => new[] { "current_position", "current_tilt_position" },
            "fan" => new[] { "percentage", "speed" },
            "humidifier" => new[] { "current_humidity", "humidity" },
            "water_heater" => new[] { "current_temperature", "temperature" },
            "sun" => new[] { "elevation", "azimuth" },
            "device_tracker" or "person" => new[] { "latitude", "longitude", "gps_accuracy" },
            "zone" => new[] { "latitude", "longitude", "radius" },
            _ => Array.Empty<string>()
        };

        // Try each candidate attribute
        foreach (var attrName in candidateAttributes)
        {
            if (attributes.TryGetValue(attrName, out var attrValue))
            {
                if (attrValue.ValueKind == JsonValueKind.Number && attrValue.TryGetDouble(out var doubleVal))
                {
                    return doubleVal;
                }
                else if (attrValue.ValueKind == JsonValueKind.String)
                {
                    var strVal = attrValue.GetString();
                    if (double.TryParse(strVal, System.Globalization.CultureInfo.InvariantCulture, out var parsedVal))
                    {
                        return parsedVal;
                    }
                }
            }
        }

        // Fallback: try to convert binary_sensor states to 0/1
        if (domain == "binary_sensor")
        {
            return state.ToLowerInvariant() switch
            {
                "on" => 1.0,
                "off" => 0.0,
                "true" => 1.0,
                "false" => 0.0,
                _ => 0.0
            };
        }

        return 0;
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

        var (hostUrl, token) = GetHostAndToken(dashboard);
        hostUrl = hostUrl.TrimEnd('/');

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token);
            
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
    public string Domain { get; init; } = string.Empty;
    public string? DeviceClass { get; init; }
    public string? UnitOfMeasurement { get; init; }
    public string? Icon { get; init; }
    public string? State { get; init; }
    public int? SupportedFeatures { get; init; }
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

public record RssFeedEntry
{
    /// <summary>
    /// Entry title
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Entry link/URL
    /// </summary>
    public string Link { get; init; } = string.Empty;

    /// <summary>
    /// Entry publication date
    /// </summary>
    public string? Published { get; init; }

    /// <summary>
    /// Entry summary/description
    /// </summary>
    public string? Summary { get; init; }
}
/// <summary>
/// Represents a historical state entry for an entity from Home Assistant's history API.
/// Includes timestamp and numeric value for graphing purposes.
/// </summary>
public record HistoryState
{
    /// <summary>
    /// The state value as a string (may be numeric, "on"/"off", etc.)
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// The state value parsed as a numeric value for graphing (0 if not numeric)
    /// </summary>
    public double NumericValue { get; init; }

    /// <summary>
    /// Timestamp when this state change occurred
    /// </summary>
    public DateTime LastChanged { get; init; }

    /// <summary>
    /// Entity attributes at this point in time
    /// </summary>
    public Dictionary<string, JsonElement> Attributes { get; init; } = new();
}