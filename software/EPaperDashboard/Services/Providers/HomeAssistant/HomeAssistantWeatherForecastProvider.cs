using System.Net.WebSockets;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers.HomeAssistant;

/// <summary>
/// Home Assistant implementation of <see cref="IWeatherForecastProvider"/>.
/// Fetches weather forecasts via the Home Assistant WebSocket API.
/// </summary>
public class HomeAssistantWeatherForecastProvider(
    HomeAssistantConnectionService connection,
    ILogger<HomeAssistantWeatherForecastProvider> logger) : IWeatherForecastProvider
{
    private readonly HomeAssistantConnectionService _connection = connection;
    private readonly ILogger<HomeAssistantWeatherForecastProvider> _logger = logger;

    public async Task<Result<Dictionary<string, object?>, string>> FetchWeatherForecastAsync(string dashboardId, string weatherEntityId, string forecastType = "daily")
    {
        var connectionInfo = _connection.GetConnectionInfo(dashboardId);
        if (connectionInfo.IsFailure)
            return connectionInfo.Error;

        if (string.IsNullOrWhiteSpace(weatherEntityId))
            return "Weather entity ID is required";

        var (hostUrl, token) = connectionInfo.Value;

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token, _connection.WebSocketPath);

            var messageId = _connection.NextMessageId();

            await HomeAssistantConnectionService.SendMessageAsync(ws, new
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

            var response = await HomeAssistantConnectionService.ReceiveMessageAsync(ws);
            _logger.LogDebug("HomeAssistant FetchWeatherForecast raw response: {Response}", response);

            var json = JsonSerializer.Deserialize<JsonElement>(response);
            var forecastData = new Dictionary<string, object?>();

            if (json.TryGetProperty("success", out var success) && success.GetBoolean() &&
                json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
            {
                JsonElement forecastArray = default;
                bool foundForecast = false;

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

                if (!foundForecast && result.TryGetProperty(weatherEntityId, out var entityObj2) &&
                    entityObj2.ValueKind == JsonValueKind.Object &&
                    entityObj2.TryGetProperty("forecast", out forecastArray) &&
                    forecastArray.ValueKind == JsonValueKind.Array)
                {
                    foundForecast = true;
                    _logger.LogDebug("Found forecast at result.{EntityId}.forecast", weatherEntityId);
                }

                if (!foundForecast && result.TryGetProperty("forecast", out forecastArray) &&
                    forecastArray.ValueKind == JsonValueKind.Array)
                {
                    foundForecast = true;
                    _logger.LogDebug("Found forecast at result.forecast");
                }

                if (foundForecast)
                {
                    var forecastList = new List<object?>();
                    foreach (var item in forecastArray.EnumerateArray())
                    {
                        var forecastItem = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in item.EnumerateObject())
                        {
                            forecastItem[prop.Name] = HomeAssistantConnectionService.ExtractJsonValue(prop.Value);
                        }
                        forecastList.Add(forecastItem);
                    }
                    forecastData["forecast"] = forecastList;
                    _logger.LogDebug("Parsed {Count} forecast items from entity {EntityId}", forecastList.Count, weatherEntityId);

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

            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None); } catch { /* using disposes socket */ }
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

    public async Task<Result<Dictionary<string, List<object?>>, string>> FetchAllWeatherForecastsAsync(string dashboardId, string forecastType = "daily")
    {
        var entityIds = await DiscoverEntitiesAsync(dashboardId, "weather");
        if (entityIds.IsFailure)
            return entityIds.Error;

        var result = new Dictionary<string, List<object?>>();
        foreach (var entityId in entityIds.Value)
        {
            var forecastResult = await FetchWeatherForecastAsync(dashboardId, entityId, forecastType);
            if (forecastResult.IsSuccess
                && forecastResult.Value.TryGetValue("forecast", out var forecastVal)
                && forecastVal is List<object?> forecastList
                && forecastList.Count > 0)
            {
                result[entityId] = forecastList;
            }
        }

        _logger.LogDebug("Fetched weather forecasts from {Count} entities for dashboard {DashboardId}", result.Count, dashboardId);
        return result;
    }

    private async Task<Result<List<string>, string>> DiscoverEntitiesAsync(string dashboardId, string domain)
    {
        var connectionInfo = _connection.GetConnectionInfo(dashboardId);
        if (connectionInfo.IsFailure)
            return connectionInfo.Error;

        var (hostUrl, token) = connectionInfo.Value;

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token, _connection.WebSocketPath);

            await HomeAssistantConnectionService.SendMessageAsync(ws, new { id = 1, type = "get_states" });
            var response = await HomeAssistantConnectionService.ReceiveMessageAsync(ws);
            var json = JsonSerializer.Deserialize<JsonElement>(response);

            var entityIds = new List<string>();
            if (json.TryGetProperty("success", out var success) && success.GetBoolean() &&
                json.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in result.EnumerateArray())
                {
                    var entityId = entity.TryGetProperty("entity_id", out var eid) ? eid.GetString() : null;
                    if (entityId != null && entityId.StartsWith(domain + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        var state = entity.TryGetProperty("state", out var s) ? s.GetString() : null;
                        if (state is not "unavailable")
                            entityIds.Add(entityId);
                    }
                }
            }

            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None); } catch { /* using disposes socket */ }
            return entityIds;
        }
        catch (Exception ex)
        {
            return $"Failed to discover {domain} entities: {ex.Message}";
        }
    }
}
