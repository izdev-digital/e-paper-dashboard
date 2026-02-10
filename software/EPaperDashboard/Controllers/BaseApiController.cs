using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LiteDB;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Controllers;

/// <summary>
/// Base controller providing common functionality for all API controllers.
/// </summary>
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from claims.
    /// Returns ObjectId.Empty if not authenticated or claim not found.
    /// </summary>
    protected ObjectId CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdValue))
            {
                return ObjectId.Empty;
            }

            try
            {
                return new ObjectId(userIdValue);
            }
            catch
            {
                return ObjectId.Empty;
            }
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
