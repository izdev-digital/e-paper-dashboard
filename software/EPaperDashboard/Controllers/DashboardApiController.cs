using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using LiteDB;
using EPaperDashboard.Services;
using EPaperDashboard.Models;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
public class DashboardApiController(DashboardService dashboardService, UserService userService) : BaseApiController
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;

    [HttpGet]
    public IActionResult GetDashboards()
    {
        ObjectId userId;
        
        // In Home Assistant mode, skip user lookup and use virtual user ID directly
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
        ObjectId objectId;
        try
        {
            objectId = new ObjectId(id);
        }
        catch
        {
            return BadRequest(new { message = "Invalid dashboard ID." });
        }

        var dashboard = _dashboardService.GetDashboardById(new ObjectId(id));
        if (dashboard.HasNoValue)
        {
            return NotFound(new { message = "Dashboard not found." });
        }

        // Verify user owns this dashboard
        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        return Ok(DashboardResponseDto.FromDashboard(dashboard.Value));
    }

    [HttpPost]
    public IActionResult CreateDashboard([FromBody] CreateDashboardRequest request)
    {
        // Verify user exists in database (skip check in Home Assistant mode)
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
            Description = request.Description ?? string.Empty,
            ApiKey = Guid.NewGuid().ToString("N")
        };

        _dashboardService.AddDashboard(dashboard);

        return Ok(DashboardResponseDto.FromDashboard(dashboard));
    }

    [HttpPut("{id}")]
    public IActionResult UpdateDashboard(string id, [FromBody] UpdateDashboardRequest request)
    {

        ObjectId objectId;
        try
        {
            objectId = new ObjectId(id);
        }
        catch
        {
            return BadRequest(new { message = "Invalid dashboard ID." });
        }

        var dashboard = _dashboardService.GetDashboardById(new ObjectId(id));
        if (dashboard.HasNoValue)
        {
            return NotFound(new { message = "Dashboard not found." });
        }

        // Verify user owns this dashboard
        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        var updatedDashboard = dashboard.Value;
        if (request.Name != null) updatedDashboard.Name = request.Name;
        if (request.Description != null) updatedDashboard.Description = request.Description;
        
        // Handle token: ClearAccessToken takes precedence over AccessToken
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
        
        // Handle rendering mode
        if (request.RenderingMode != null)
        {
            if (Enum.TryParse<RenderingMode>(request.RenderingMode, out var renderingMode))
            {
                updatedDashboard.RenderingMode = renderingMode;
            }
        }

        _dashboardService.UpdateDashboard(updatedDashboard);

        return Ok(DashboardResponseDto.FromDashboard(updatedDashboard));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteDashboard(string id)
    {
        ObjectId objectId;
        try
        {
            objectId = new ObjectId(id);
        }
        catch
        {
            return BadRequest(new { message = "Invalid dashboard ID." });
        }

        var dashboard = _dashboardService.GetDashboardById(new ObjectId(id));
        if (dashboard.HasNoValue)
        {
            return NotFound(new { message = "Dashboard not found." });
        }

        // Verify user owns this dashboard
        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        _dashboardService.DeleteDashboard(new ObjectId(id));

        return Ok(new { message = "Dashboard deleted successfully." });
    }
}

public record CreateDashboardRequest(string Name, string? Description);

public record UpdateDashboardRequest(
    string? Name,
    string? Description,
    string? AccessToken,
    bool? ClearAccessToken,
    string? Host,
    string? Path,
    List<TimeOnly>? UpdateTimes,
    LayoutConfig? LayoutConfig,
    string? RenderingMode
);

// DTO that hides the actual access token from the frontend (only exposes whether one is set)
public record DashboardResponseDto(
    string Id,
    string Name,
    string Description,
    string ApiKey,
    string UserId,
    bool HasAccessToken,
    string? Host,
    string? Path,
    List<TimeOnly>? UpdateTimes,
    LayoutConfig? LayoutConfig,
    string? RenderingMode
)
{
    public static DashboardResponseDto FromDashboard(Dashboard dashboard) => new(
        Id: dashboard.Id.ToString(),
        Name: dashboard.Name,
        Description: dashboard.Description,
        ApiKey: dashboard.ApiKey,
        UserId: dashboard.UserId.ToString(),
        HasAccessToken: !string.IsNullOrWhiteSpace(dashboard.AccessToken),
        Host: dashboard.Host,
        Path: dashboard.Path,
        UpdateTimes: dashboard.UpdateTimes,
        LayoutConfig: dashboard.LayoutConfig,
        RenderingMode: dashboard.RenderingMode.ToString()
    );
}
