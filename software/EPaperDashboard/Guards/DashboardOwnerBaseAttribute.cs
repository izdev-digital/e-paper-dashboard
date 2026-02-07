using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using LiteDB;
using EPaperDashboard.Services;

namespace EPaperDashboard.Guards;

/// <summary>
/// Base class for dashboard ownership authorization filters.
/// Contains common validation logic for checking if the authenticated user owns a dashboard.
/// </summary>
public abstract class DashboardOwnerBaseAttribute : Attribute
{
    /// <summary>
    /// Validates that the current user owns the specified dashboard.
    /// </summary>
    /// <param name="context">The authorization filter context</param>
    /// <param name="dashboardId">The dashboard ID to validate</param>
    protected void ValidateDashboardOwnership(AuthorizationFilterContext context, string? dashboardId)
    {
        if (string.IsNullOrWhiteSpace(dashboardId))
        {
            context.Result = new BadRequestObjectResult(new { error = "Dashboard ID is required" });
            return;
        }

        // Get user ID from claims
        var userIdValue = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        ObjectId userId;
        try
        {
            userId = new ObjectId(userIdValue);
        }
        catch
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get services from DI
        var userService = context.HttpContext.RequestServices.GetService<UserService>();
        var dashboardService = context.HttpContext.RequestServices.GetService<DashboardService>();

        if (userService == null || dashboardService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Verify user exists
        var user = userService.GetUserById(userId);
        if (user.HasNoValue)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Parse and validate dashboard ID
        ObjectId dashboardObjectId;
        try
        {
            dashboardObjectId = new ObjectId(dashboardId);
        }
        catch
        {
            context.Result = new BadRequestObjectResult(new { error = "Invalid dashboard ID." });
            return;
        }

        // Get dashboard
        var dashboardMaybe = dashboardService.GetDashboardById(dashboardObjectId);
        if (dashboardMaybe.HasNoValue)
        {
            context.Result = new NotFoundObjectResult(new { error = "Dashboard not found." });
            return;
        }

        // Check ownership
        if (dashboardMaybe.Value.UserId != user.Value.Id)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Authorization successful - context.Result remains null, continue to action
    }
}
