using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using LiteDB;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Rendering;

namespace EPaperDashboard.Controllers;

/// <summary>
/// Serves dashboards as self-contained static HTML pages (server-side rendered).
/// Uses the same cookie auth as the Angular frontend.
/// </summary>
[ApiController]
[Route("api/dashboards")]
[Authorize]
public class DashboardSsrController(
    DashboardService dashboardService,
    DashboardHtmlRenderingService htmlRenderingService,
    UserService userService) : ControllerBase
{
    private ObjectId UserId => new(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

    /// <summary>
    /// Returns the dashboard rendered as a self-contained HTML page with live data from Home Assistant.
    /// </summary>
    [HttpGet("{id}/render-html")]
    public async Task<IActionResult> RenderDashboardHtml(string id)
    {
        ObjectId objectId;
        try
        {
            objectId = new ObjectId(id);
        }
        catch
        {
            return BadRequest("Invalid dashboard ID");
        }

        var user = userService.GetUserById(UserId);
        if (user.HasNoValue)
            return Unauthorized();

        var dashboard = dashboardService.GetDashboardById(objectId);
        if (dashboard.HasNoValue)
            return NotFound("Dashboard not found");

        if (dashboard.Value.UserId != user.Value.Id)
            return Forbid();

        if (dashboard.Value.LayoutConfig == null)
            return BadRequest("Dashboard has no layout configuration. Open the designer and create a layout first.");

        try
        {
            // Serialize LayoutConfig object to JSON with camelCase naming for JavaScript compatibility
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var layoutConfigJson = System.Text.Json.JsonSerializer.Serialize(dashboard.Value.LayoutConfig, serializerOptions);
            
            var html = await htmlRenderingService.RenderDashboardHtmlAsync(
                dashboard.Value.Id.ToString(),
                layoutConfigJson);

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to render dashboard: {ex.Message}");
        }
    }
}
