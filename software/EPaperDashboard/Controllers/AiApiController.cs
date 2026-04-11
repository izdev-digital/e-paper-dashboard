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
    UserService userService,
    DashboardService dashboardService,
    AiDashboardGenerationService aiGenerationService,
    HomeAssistantConnectionService homeAssistantConnectionService) : BaseApiController
{
    [HttpGet("config")]
    public IActionResult GetAiConfig()
    {
        var user = userService.GetUserById(CurrentUserId);
        if (user.HasNoValue)
        {
            return NotFound("User not found");
        }

        return Ok(user.Value.AiConfig ?? new AiConfig());
    }

    [HttpPut("config")]
    public IActionResult UpdateAiConfig([FromBody] AiConfig config)
    {
        var user = userService.GetUserById(CurrentUserId);
        if (user.HasNoValue)
        {
            return NotFound("User not found");
        }

        var validationError = ValidateAiConfig(config);
        if (validationError != null)
            return BadRequest(validationError);

        user.Value.AiConfig = config;
        userService.UpdateUser(user.Value);

        return Ok(config);
    }

    private static string? ValidateAiConfig(AiConfig config)
    {
        return config.ConnectionMode switch
        {
            AiConnectionMode.Direct when string.IsNullOrWhiteSpace(config.DirectEndpoint)
                => "Direct endpoint URL is required",
            AiConnectionMode.Direct when string.IsNullOrWhiteSpace(config.DirectModel)
                => "Model name is required for direct connections",
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
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return BadRequest("Invalid dashboard ID");
        }

        var dashboard = dashboardService.GetDashboardById(id);
        if (dashboard.HasNoValue)
        {
            return NotFound("Dashboard not found");
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        if (!dashboard.Value.IsAiEnabled)
        {
            return BadRequest("AI is not enabled for this dashboard");
        }

        var effectivePrompt = request?.Prompt ?? dashboard.Value.AiPrompt;
        if (string.IsNullOrWhiteSpace(effectivePrompt))
        {
            return BadRequest("AI prompt is not configured for this dashboard");
        }

        var result = await aiGenerationService.GenerateAsync(
            dashboard.Value, request?.Prompt, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new
            {
                widgets = result.Value.Widgets,
                generatedAt = dashboard.Value.LastAiGenerationTime,
                dataSummary = result.Value.DataSummary,
                promptTokenEstimate = result.Value.PromptTokenEstimate
            });
        }

        return BadRequest(new { message = result.Error });
    }

    [HttpGet("dashboards/{dashboardId}/generated")]
    public IActionResult GetGeneratedWidgets(string dashboardId)
    {
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return BadRequest("Invalid dashboard ID");
        }

        var dashboard = dashboardService.GetDashboardById(id);
        if (dashboard.HasNoValue)
        {
            return NotFound("Dashboard not found");
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        return Ok(new
        {
            widgets = dashboard.Value.AiGeneratedWidgets ?? new List<WidgetConfig>(),
            generatedAt = dashboard.Value.LastAiGenerationTime,
            isAiEnabled = dashboard.Value.IsAiEnabled,
            prompt = dashboard.Value.AiPrompt,
            lastError = dashboard.Value.LastAiGenerationError
        });
    }

    [HttpDelete("dashboards/{dashboardId}/generated")]
    public IActionResult ClearGeneratedWidgets(string dashboardId)
    {
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return BadRequest("Invalid dashboard ID");
        }

        var dashboard = dashboardService.GetDashboardById(id);
        if (dashboard.HasNoValue)
        {
            return NotFound("Dashboard not found");
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        dashboard.Value.AiGeneratedWidgets = null;
        dashboard.Value.LastAiGenerationTime = null;
        dashboardService.UpdateDashboard(dashboard.Value);

        return NoContent();
    }

    [HttpGet("conversation-agents")]
    public async Task<IActionResult> GetConversationAgents(CancellationToken cancellationToken)
    {
        var dashboards = dashboardService.GetDashboardsForUser(CurrentUserId);
        var dashboardId = dashboards
            .Select(d => d.Id.ToString())
            .FirstOrDefault(id => homeAssistantConnectionService.GetConnectionInfo(id).IsSuccess);

        if (dashboardId is null)
        {
            return BadRequest("No dashboard with a valid Home Assistant connection found. Please connect a dashboard to Home Assistant first.");
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
}

public record GenerateAiRequest(string? Prompt = null);
