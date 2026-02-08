using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace EPaperDashboard.Guards;

/// <summary>
/// Authorization filter that ensures the authenticated user owns the dashboard specified in the request body.
/// Expects a JSON body with a "dashboardId" property (case-insensitive).
/// </summary>
public class DashboardOwnerFromBodyAttribute : DashboardOwnerBaseAttribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Enable buffering to read the body multiple times
        context.HttpContext.Request.EnableBuffering();

        string? dashboardId = null;

        try
        {
            // Read the body
            context.HttpContext.Request.Body.Position = 0;
            using var reader = new StreamReader(context.HttpContext.Request.Body, leaveOpen: true);
            var bodyContent = await reader.ReadToEndAsync();
            context.HttpContext.Request.Body.Position = 0; // Reset for model binding

            if (!string.IsNullOrWhiteSpace(bodyContent))
            {
                // Parse JSON and extract dashboardId
                using var doc = JsonDocument.Parse(bodyContent);
                if (doc.RootElement.TryGetProperty("dashboardId", out var dashboardIdElement) ||
                    doc.RootElement.TryGetProperty("DashboardId", out dashboardIdElement))
                {
                    dashboardId = dashboardIdElement.GetString();
                }
            }
        }
        catch
        {
            context.Result = new BadRequestObjectResult(new { error = "Invalid request body" });
            return;
        }

        // Validate dashboard ownership using base class
        ValidateDashboardOwnership(context, dashboardId);
    }
}
