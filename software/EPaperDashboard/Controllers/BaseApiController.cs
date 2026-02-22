using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Controllers;

/// <summary>
/// Base controller providing common functionality for all API controllers.
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from claims.
    /// In Home Assistant mode, returns a virtual user ID.
    /// Returns Guid.Empty if not authenticated or claim not found.
    /// </summary>
    protected Guid CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdValue))
            {
                return Guid.Empty;
            }

            // In Home Assistant ingress mode, use virtual user ID
            if (IsHomeAssistantIngress && userIdValue == Constants.HomeAssistantAdminUserId)
            {
                return Constants.HomeAssistantVirtualUserId;
            }

            if (Guid.TryParse(userIdValue, out var guid))
            {
                return guid;
            }

            return Guid.Empty;
        }
    }

    /// <summary>
    /// Gets whether the current user is a super user.
    /// </summary>
    protected bool IsSuperUser => 
        User.FindFirst(Constants.IsSuperUserClaim)?.Value == "true";

    /// <summary>
    /// Gets whether the current user is authenticated via Home Assistant ingress.
    /// </summary>
    protected bool IsHomeAssistantIngress => 
        User.FindFirst(Constants.HomeAssistantIngressClaim)?.Value == "true";

    /// <summary>
    /// Gets the current user's username from claims.
    /// </summary>
    protected string? CurrentUsername => 
        User.FindFirst(ClaimTypes.Name)?.Value;
}

