using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EPaperDashboard.Guards;

/// <summary>
/// Authorization filter that ensures the authenticated user owns the dashboard specified in the route.
/// Expects a route parameter named "dashboardId".
/// </summary>
public class DashboardOwnerAttribute : DashboardOwnerBaseAttribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Get dashboardId from route
        if (!context.RouteData.Values.TryGetValue("dashboardId", out var dashboardIdObj) || 
            dashboardIdObj is not string dashboardId ||
            string.IsNullOrWhiteSpace(dashboardId))
        {
            context.Result = new BadRequestObjectResult(new { error = "Dashboard ID is required" });
            return;
        }

        // Validate dashboard ownership using base class
        ValidateDashboardOwnership(context, dashboardId);
    }
}
