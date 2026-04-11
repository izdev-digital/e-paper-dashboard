using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Ai;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public sealed class AiApiController(
    DashboardService dashboardService,
    UserService userService,
    AiDashboardGenerationService aiGenerationService,
    HomeAssistantConnectionService homeAssistantConnectionService) : BaseApiController
{
    [HttpGet("config")]
    public IActionResult GetGlobalAiConfig()
    {
        var user = userService.GetUserById(CurrentUserId);
        if (user.HasNoValue)
        {
            return NotFound("User not found");
        }

        return Ok(user.Value.AiConfig ?? new AiConfig());
    }

    [HttpPut("config")]
    public IActionResult UpdateGlobalAiConfig([FromBody] AiConfig config)
    {
        var user = userService.GetUserById(CurrentUserId);
        if (user.HasNoValue)
        {
            return NotFound("User not found");
        }

        var validationError = ValidateGlobalAiConfig(config);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        user.Value.AiConfig = config;
        userService.UpdateUser(user.Value);

        if (config.ConnectionMode == AiConnectionMode.None)
        {
            DisableAiOnDashboardsWithoutOverride(user.Value.Id);
        }

        return Ok(config);
    }

    private static string? ValidateGlobalAiConfig(AiConfig config)
    {
        return config.ConnectionMode switch
        {
            AiConnectionMode.Direct when string.IsNullOrWhiteSpace(config.DirectEndpoint)
                => "Direct endpoint URL is required",
            AiConnectionMode.Direct when string.IsNullOrWhiteSpace(config.DirectModel)
                => "Model name is required for direct connections",
            _ => null
        };
    }

    [HttpGet("dashboards/{dashboardId}/config")]
    public IActionResult GetAiConfig(string dashboardId)
    {
        var dashboard = GetOwnedDashboard(dashboardId);
        if (dashboard == null)
        {
            return NotFound("Dashboard not found");
        }

        return Ok(dashboard.AiConfig ?? new AiConfig());
    }

    [HttpPut("dashboards/{dashboardId}/config")]
    public IActionResult UpdateAiConfig(string dashboardId, [FromBody] AiConfig config)
    {
        var dashboard = GetOwnedDashboard(dashboardId);
        if (dashboard == null)
        {
            return NotFound("Dashboard not found");
        }

        var validationError = ValidateAiConfig(config);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        dashboard.AiConfig = config;

        if (config.ConnectionMode == AiConnectionMode.None && dashboard.IsAiEnabled)
        {
            var user = userService.GetUserById(CurrentUserId);
            var globalConfig = user.HasValue ? user.Value.AiConfig : null;
            if (globalConfig == null || globalConfig.ConnectionMode == AiConnectionMode.None)
            {
                dashboard.IsAiEnabled = false;
            }
        }

        dashboardService.UpdateDashboard(dashboard);

        return Ok(config);
    }

    private static string? ValidateAiConfig(AiConfig config)
    {
        return config.ConnectionMode switch
        {
            AiConnectionMode.Direct
                => "Direct AI configuration is only supported as a global setting. Use the global AI Config page instead.",
            AiConnectionMode.HomeAssistant when string.IsNullOrWhiteSpace(config.HomeAssistantAgentId)
                => "Home Assistant conversation agent must be selected",
            _ => null
        };
    }

    [HttpPost("dashboards/{dashboardId}/generate")]
    public async Task<IActionResult> GenerateAiDashboard(
        string dashboardId,
        [FromBody] GenerateAiRequest? request,
        CancellationToken cancellationToken)
    {
        var dashboard = GetOwnedDashboard(dashboardId);
        if (dashboard == null)
        {
            return NotFound("Dashboard not found");
        }

        if (!dashboard.IsAiEnabled)
        {
            return BadRequest("AI is not enabled for this dashboard");
        }

        if (!HasEffectiveAiConfig(dashboard))
        {
            return BadRequest("AI is not configured. Set up an AI provider in Settings or the dashboard.");
        }

        var effectivePrompt = request?.Prompt ?? dashboard.AiPrompt;
        if (string.IsNullOrWhiteSpace(effectivePrompt))
        {
            return BadRequest("AI prompt is not configured for this dashboard");
        }

        var result = await aiGenerationService.GenerateAsync(
            dashboard, request?.Prompt, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new
            {
                widgets = result.Value.Widgets,
                generatedAt = dashboard.LastAiGenerationTime,
                dataSummary = result.Value.DataSummary,
                promptTokenEstimate = result.Value.PromptTokenEstimate
            });
        }

        return BadRequest(new { message = result.Error });
    }

    [HttpGet("dashboards/{dashboardId}/generated")]
    public IActionResult GetGeneratedWidgets(string dashboardId)
    {
        var dashboard = GetOwnedDashboard(dashboardId);
        if (dashboard == null)
        {
            return NotFound("Dashboard not found");
        }

        return Ok(new
        {
            widgets = dashboard.AiGeneratedWidgets ?? new List<WidgetConfig>(),
            generatedAt = dashboard.LastAiGenerationTime,
            isAiEnabled = dashboard.IsAiEnabled,
            prompt = dashboard.AiPrompt,
            lastError = dashboard.LastAiGenerationError
        });
    }

    [HttpDelete("dashboards/{dashboardId}/generated")]
    public IActionResult ClearGeneratedWidgets(string dashboardId)
    {
        var dashboard = GetOwnedDashboard(dashboardId);
        if (dashboard == null)
        {
            return NotFound("Dashboard not found");
        }

        dashboard.AiGeneratedWidgets = null;
        dashboard.LastAiGenerationTime = null;
        dashboardService.UpdateDashboard(dashboard);

        return NoContent();
    }

    [HttpGet("dashboards/{dashboardId}/conversation-agents")]
    public async Task<IActionResult> GetConversationAgents(string dashboardId, CancellationToken cancellationToken)
    {
        var dashboard = GetOwnedDashboard(dashboardId);
        if (dashboard == null)
        {
            return NotFound("Dashboard not found");
        }

        var connectionInfo = homeAssistantConnectionService.GetConnectionInfo(dashboardId);
        if (connectionInfo.IsFailure)
        {
            return BadRequest("This dashboard does not have a valid Home Assistant connection. Please configure Host and Access Token first.");
        }

        try
        {
            using var ws = await homeAssistantConnectionService.ConnectAsync(dashboardId);

            var message = new Dictionary<string, object>
            {
                ["id"] = homeAssistantConnectionService.NextMessageId(),
                ["type"] = "conversation/agent/list",
                ["language"] = "en"
            };

            await HomeAssistantConnectionService.SendMessageAsync(ws, message);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
            var responseStr = await HomeAssistantConnectionService.ReceiveMessageAsync(ws, timeoutCts.Token);

            await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            using var doc = JsonDocument.Parse(responseStr);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean()
                && root.TryGetProperty("result", out var result)
                && result.TryGetProperty("agents", out var agents))
            {
                var agentList = new List<object>();
                foreach (var agent in agents.EnumerateArray())
                {
                    var id = agent.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var name = agent.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (id is not null)
                    {
                        agentList.Add(new { id, name = name ?? id });
                    }
                }
                return Ok(agentList);
            }

            return Ok(Array.Empty<object>());
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, "Home Assistant request timed out");
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to fetch conversation agents: {ex.Message}");
        }
    }

    private Dashboard? GetOwnedDashboard(string dashboardId)
    {
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return null;
        }

        var dashboard = dashboardService.GetDashboardById(id);
        if (dashboard.HasNoValue || dashboard.Value.UserId != CurrentUserId)
        {
            return null;
        }

        return dashboard.Value;
    }

    private void DisableAiOnDashboardsWithoutOverride(UserId userId)
    {
        var dashboards = dashboardService.GetDashboardsForUser(userId);
        foreach (var dashboard in dashboards)
        {
            if (!dashboard.IsAiEnabled)
            {
                continue;
            }

            var hasOverride = dashboard.AiConfig != null
                && dashboard.AiConfig.ConnectionMode == AiConnectionMode.HomeAssistant;
            if (!hasOverride)
            {
                dashboard.IsAiEnabled = false;
                dashboardService.UpdateDashboard(dashboard);
            }
        }
    }

    private bool HasEffectiveAiConfig(Dashboard dashboard)
    {
        if (dashboard.AiConfig != null && dashboard.AiConfig.ConnectionMode == AiConnectionMode.HomeAssistant)
        {
            return true;
        }

        var user = userService.GetUserById(dashboard.UserId);
        return user.HasValue
            && user.Value.AiConfig != null
            && user.Value.AiConfig.ConnectionMode != AiConnectionMode.None;
    }
}

public record GenerateAiRequest(string? Prompt = null);
