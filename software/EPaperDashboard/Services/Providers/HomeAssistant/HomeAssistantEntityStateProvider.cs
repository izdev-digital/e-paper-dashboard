using System.Net.WebSockets;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers.HomeAssistant;

/// <summary>
/// Home Assistant implementation of <see cref="IEntityStateProvider"/>.
/// Fetches entity states via the Home Assistant WebSocket API.
/// </summary>
public class HomeAssistantEntityStateProvider(
    HomeAssistantConnectionService connection,
    ILogger<HomeAssistantEntityStateProvider> logger) : IEntityStateProvider
{
    private readonly HomeAssistantConnectionService _connection = connection;
    private readonly ILogger<HomeAssistantEntityStateProvider> _logger = logger;

    public async Task<Result<List<HassEntityState>, string>> FetchEntityStatesAsync(string dashboardId, string[] entityIds)
    {
        var connectionInfo = _connection.GetConnectionInfo(dashboardId);
        if (connectionInfo.IsFailure)
            return connectionInfo.Error;

        var requestedIds = new HashSet<string>(entityIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        if (requestedIds.Count == 0)
            return Result.Success<List<HassEntityState>, string>(new List<HassEntityState>());

        var (hostUrl, token) = connectionInfo.Value;

        try
        {
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(hostUrl, token, _connection.WebSocketPath);

            await HomeAssistantConnectionService.SendMessageAsync(ws, new
            {
                id = 1,
                type = "get_states"
            });

            var statesResponse = await HomeAssistantConnectionService.ReceiveMessageAsync(ws);
            var statesResult = JsonSerializer.Deserialize<JsonElement>(statesResponse);

            var entityStates = new List<HassEntityState>();

            if (statesResult.TryGetProperty("success", out var success) && success.GetBoolean() &&
                statesResult.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in result.EnumerateArray())
                {
                    var entityId = entity.TryGetProperty("entity_id", out var eid) ? eid.GetString() : null;
                    if (string.IsNullOrWhiteSpace(entityId) || !requestedIds.Contains(entityId))
                        continue;

                    var state = entity.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : string.Empty;
                    var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    if (entity.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var attr in attrs.EnumerateObject())
                        {
                            attributes[attr.Name] = HomeAssistantConnectionService.ExtractJsonValue(attr.Value);
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
}
