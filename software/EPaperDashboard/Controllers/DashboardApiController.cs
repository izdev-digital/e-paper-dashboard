using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EPaperDashboard.Services;
using EPaperDashboard.Models;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
public class DashboardApiController(DashboardService dashboardService, UserService userService, IDeploymentStrategy deploymentStrategy) : BaseApiController
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;
    private readonly IDeploymentStrategy _deploymentStrategy = deploymentStrategy;

    [HttpGet]
    public IActionResult GetDashboards()
    {
        UserId userId;
        
        if (IsHomeAssistantIngress)
        {
            userId = CurrentUserId;
        }
        else
        {
            var user = _userService.GetUserById(CurrentUserId);
            if (user.HasNoValue)
            {
                return Unauthorized();
            }
            userId = user.Value.Id;
        }

        var dashboards = _dashboardService.GetDashboardsForUser(userId);
        return Ok(dashboards);
    }

    [HttpGet("{id}")]
    public IActionResult GetDashboard(string id)
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
        {
            return BadRequest(new { message = "Invalid dashboard ID." });
        }

        var dashboard = _dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
        {
            return NotFound(new { message = "Dashboard not found." });
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        return Ok(DashboardResponseDto.FromDashboard(dashboard.Value, _deploymentStrategy.IsAutoConnected));
    }

    [HttpPost]
    public IActionResult CreateDashboard([FromBody] CreateDashboardRequest request)
    {
        if (!IsHomeAssistantIngress)
        {
            var user = _userService.GetUserById(CurrentUserId);
            if (user.HasNoValue)
            {
                return Unauthorized();
            }
        }

        var dashboard = new Dashboard
        {
            UserId = CurrentUserId,
            Name = request.Name,
            Description = request.Description ?? string.Empty
        };

        if (request.Orientation != null && Enum.TryParse<DashboardOrientation>(request.Orientation, out var orientation))
        {
            dashboard.Orientation = orientation;
        }

        if (request.ScreenWidth.HasValue && request.ScreenHeight.HasValue)
        {
            if (!DashboardSizePreset.IsValidSize(request.ScreenWidth.Value, request.ScreenHeight.Value))
            {
                return BadRequest(new { message = "Invalid dashboard size. Please select a supported size." });
            }
            var preset = DashboardSizePreset.FindByDimensions(request.ScreenWidth.Value, request.ScreenHeight.Value)!;
            dashboard.ScreenWidth = preset.Width;
            dashboard.ScreenHeight = preset.Height;
        }

        _dashboardService.AddDashboard(dashboard);

        return Ok(DashboardResponseDto.FromDashboard(dashboard, _deploymentStrategy.IsAutoConnected));
    }

    [HttpPut("{id}")]
    public IActionResult UpdateDashboard(string id, [FromBody] UpdateDashboardRequest request)
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
        {
            return BadRequest(new { message = "Invalid dashboard ID." });
        }

        var dashboard = _dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
        {
            return NotFound(new { message = "Dashboard not found." });
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        var updatedDashboard = dashboard.Value;
        if (request.Name != null) updatedDashboard.Name = request.Name;
        if (request.Description != null) updatedDashboard.Description = request.Description;
        
        if (request.ClearAccessToken == true)
        {
            updatedDashboard.AccessToken = null;
        }
        else if (request.AccessToken != null)
        {
            updatedDashboard.AccessToken = request.AccessToken;
        }
        
        if (request.Host != null) updatedDashboard.Host = request.Host;
        if (request.Path != null) updatedDashboard.Path = request.Path;
        if (request.UpdateTimes != null) updatedDashboard.UpdateTimes = request.UpdateTimes;
        if (request.LayoutConfig != null) updatedDashboard.LayoutConfig = request.LayoutConfig;
        
        if (request.RenderingMode != null)
        {
            if (Enum.TryParse<RenderingMode>(request.RenderingMode, out var renderingMode))
            {
                updatedDashboard.RenderingMode = renderingMode;
            }
        }

        if (request.Orientation != null)
        {
            if (Enum.TryParse<DashboardOrientation>(request.Orientation, out var orientation))
            {
                updatedDashboard.Orientation = orientation;
            }
        }

        if (request.ScreenWidth.HasValue && request.ScreenHeight.HasValue)
        {
            if (!DashboardSizePreset.IsValidSize(request.ScreenWidth.Value, request.ScreenHeight.Value))
            {
                return BadRequest(new { message = "Invalid dashboard size. Please select a supported size." });
            }
            var preset = DashboardSizePreset.FindByDimensions(request.ScreenWidth.Value, request.ScreenHeight.Value)!;
            updatedDashboard.ScreenWidth = preset.Width;
            updatedDashboard.ScreenHeight = preset.Height;
        }

        if (request.IsAiEnabled.HasValue) updatedDashboard.IsAiEnabled = request.IsAiEnabled.Value;
        if (request.AiPrompt != null) updatedDashboard.AiPrompt = request.AiPrompt;
        if (request.AiDataSourceEntityIds != null) updatedDashboard.AiDataSourceEntityIds = request.AiDataSourceEntityIds;
        if (request.AiLeadTimeMinutes.HasValue) updatedDashboard.AiLeadTimeMinutes = request.AiLeadTimeMinutes.Value;

        _dashboardService.UpdateDashboard(updatedDashboard);

        return Ok(DashboardResponseDto.FromDashboard(updatedDashboard, _deploymentStrategy.IsAutoConnected));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteDashboard(string id)
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
        {
            return BadRequest(new { message = "Invalid dashboard ID." });
        }

        var dashboard = _dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
        {
            return NotFound(new { message = "Dashboard not found." });
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        _dashboardService.DeleteDashboard(dashboardId);

        return Ok(new { message = "Dashboard deleted successfully." });
    }
}

public record CreateDashboardRequest(string Name, string? Description, string? Orientation, int? ScreenWidth, int? ScreenHeight);

public record UpdateDashboardRequest(
    string? Name,
    string? Description,
    string? AccessToken,
    bool? ClearAccessToken,
    string? Host,
    string? Path,
    List<TimeOnly>? UpdateTimes,
    LayoutConfig? LayoutConfig,
    string? RenderingMode,
    string? Orientation,
    int? ScreenWidth,
    int? ScreenHeight,
    bool? IsAiEnabled,
    string? AiPrompt,
    List<string>? AiDataSourceEntityIds,
    int? AiLeadTimeMinutes
);

public record DashboardResponseDto(
    string Id,
    string Name,
    string Description,
    string UserId,
    bool HasAccessToken,
    string? Host,
    string? Path,
    List<TimeOnly>? UpdateTimes,
    LayoutConfig? LayoutConfig,
    string? RenderingMode,
    string Orientation,
    int ScreenWidth,
    int ScreenHeight,
    bool IsAiEnabled,
    string? AiPrompt,
    List<string>? AiDataSourceEntityIds,
    int AiLeadTimeMinutes,
    DateTimeOffset? LastAiGenerationTime
)
{
    public static DashboardResponseDto FromDashboard(Dashboard dashboard, bool isAutoConnected = false) => new(
        Id: dashboard.Id.ToString(),
        Name: dashboard.Name,
        Description: dashboard.Description,
        UserId: dashboard.UserId.ToString(),
        HasAccessToken: isAutoConnected || !string.IsNullOrWhiteSpace(dashboard.AccessToken),
        Host: dashboard.Host,
        Path: dashboard.Path,
        UpdateTimes: dashboard.UpdateTimes,
        LayoutConfig: dashboard.LayoutConfig,
        RenderingMode: dashboard.RenderingMode.ToString(),
        Orientation: dashboard.Orientation.ToString(),
        ScreenWidth: dashboard.ScreenWidth,
        ScreenHeight: dashboard.ScreenHeight,
        IsAiEnabled: dashboard.IsAiEnabled,
        AiPrompt: dashboard.AiPrompt,
        AiDataSourceEntityIds: dashboard.AiDataSourceEntityIds,
        AiLeadTimeMinutes: dashboard.AiLeadTimeMinutes,
        LastAiGenerationTime: dashboard.LastAiGenerationTime
    );
}
