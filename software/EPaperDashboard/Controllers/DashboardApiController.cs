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
public class DashboardApiController(DashboardService dashboardService, UserService userService) : ControllerBase
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;

    private ObjectId UserId => new(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

    [HttpGet]
    public IActionResult GetDashboards()
    {
        var user = _userService.GetUserById(UserId);
        if (user.HasNoValue)
        {
            return Unauthorized();
        }

        var dashboards = _dashboardService.GetDashboardsForUser(user.Value.Id);
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
        var user = _userService.GetUserById(UserId);
        if (user.HasNoValue || dashboard.Value.UserId != user.Value.Id)
        {
            return Forbid();
        }

        return Ok(dashboard.Value);
    }

    [HttpPost]
    public IActionResult CreateDashboard([FromBody] CreateDashboardRequest request)
    {
        var user = _userService.GetUserById(UserId);
        if (user.HasNoValue)
        {
            return Unauthorized();
        }

        var dashboard = new Dashboard
        {
            UserId = user.Value.Id,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            ApiKey = Guid.NewGuid().ToString("N")
        };

        _dashboardService.AddDashboard(dashboard);

        return Ok(dashboard);
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
        var user = _userService.GetUserById(UserId);
        if (user.HasNoValue || dashboard.Value.UserId != user.Value.Id)
        {
            return Forbid();
        }

        var updatedDashboard = dashboard.Value;
        if (request.Name != null) updatedDashboard.Name = request.Name;
        if (request.Description != null) updatedDashboard.Description = request.Description;
        if (request.AccessToken != null) updatedDashboard.AccessToken = request.AccessToken;
        if (request.Host != null) updatedDashboard.Host = request.Host;
        if (request.Path != null) updatedDashboard.Path = request.Path;
        if (request.UpdateTimes != null) updatedDashboard.UpdateTimes = request.UpdateTimes;

        _dashboardService.UpdateDashboard(updatedDashboard);

        return Ok(updatedDashboard);
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
        var user = _userService.GetUserById(UserId);
        if (user.HasNoValue || dashboard.Value.UserId != user.Value.Id)
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
    string? Host,
    string? Path,
    List<TimeOnly>? UpdateTimes
);
