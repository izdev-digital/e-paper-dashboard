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
    IAiServiceFactory aiServiceFactory,
    HomeAssistantConnectionService homeAssistantConnectionService) : BaseApiController
{
    /// <summary>
    /// Gets the current user's AI configuration.
    /// </summary>
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

    /// <summary>
    /// Updates the current user's AI configuration.
    /// </summary>
    [HttpPut("config")]
    public IActionResult UpdateAiConfig([FromBody] AiConfig config)
    {
        var user = userService.GetUserById(CurrentUserId);
        if (user.HasNoValue)
        {
            return NotFound("User not found");
        }

        // Validate required fields per connection mode
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

    /// <summary>
    /// Triggers AI generation for a specific dashboard. Returns the generated widgets.
    /// </summary>
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

        // When a prompt override is provided, use it directly; otherwise require the saved prompt
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

    /// <summary>
    /// Gets the current AI-generated widgets for a dashboard.
    /// </summary>
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

    /// <summary>
    /// Clears the AI-generated widgets for a dashboard.
    /// </summary>
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

    /// <summary>
    /// Lists available Home Assistant conversation agents.
    /// Finds a HA-connected dashboard owned by the current user to establish the connection.
    /// </summary>
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
    /// <summary>
    /// Generates content for an ai-content widget using the user's AI configuration.
    /// </summary>
    [HttpPost("widget-content/generate")]
    public async Task<IActionResult> GenerateWidgetContent(
        [FromBody] GenerateWidgetContentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { message = "Prompt is required" });
        }

        if (!DashboardId.TryParse(request.DashboardId, out var id))
        {
            return BadRequest(new { message = "Invalid dashboard ID" });
        }

        var dashboard = dashboardService.GetDashboardById(id);
        if (dashboard.HasNoValue || dashboard.Value.UserId != CurrentUserId)
        {
            return NotFound(new { message = "Dashboard not found" });
        }

        var user = userService.GetUserById(CurrentUserId);
        if (user.HasNoValue || user.Value.AiConfig == null
            || user.Value.AiConfig.ConnectionMode == AiConnectionMode.None)
        {
            return BadRequest(new { message = "AI is not configured. Set up an AI connection in user settings." });
        }

        var aiServiceResult = aiServiceFactory.Create(user.Value.AiConfig, request.DashboardId);
        if (aiServiceResult.IsFailure)
        {
            return BadRequest(new { message = aiServiceResult.Error });
        }

        const string systemPrompt = """
            You are an e-paper dashboard content writer. Generate content based on the user's prompt.
            Return ONLY the content text — no JSON wrapping, no code fences.
            Use basic markdown formatting: headings (#-####), **bold**, *italic*, lists, blockquotes.
            Keep content concise and suitable for a small e-paper display widget.
            """;

        var result = await aiServiceResult.Value.GenerateCompletionAsync(
            systemPrompt, request.Prompt, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(new { content = result.Value });
    }
}

public record GenerateAiRequest(string? Prompt = null);
public record GenerateWidgetContentRequest(string DashboardId, string Prompt);
