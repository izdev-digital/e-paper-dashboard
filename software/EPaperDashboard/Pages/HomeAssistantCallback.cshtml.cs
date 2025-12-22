using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Services;

namespace EPaperDashboard.Pages;

[AllowAnonymous]
public class HomeAssistantCallbackModel(
    HomeAssistantAuthService authService,
    ILogger<HomeAssistantCallbackModel> logger) : PageModel
{
    private readonly HomeAssistantAuthService _authService = authService;
    private readonly ILogger<HomeAssistantCallbackModel> _logger = logger;

    public string? DashboardId { get; private set; }
    public string? AccessToken { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var result = await _authService.HandleCallback(code, state, error);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Home Assistant callback failed: {Error}", result.Error);
            
            DashboardId = result.DashboardId;
            ErrorMessage = result.Error;
            return Page();
        }

        return RedirectToPage("/Dashboards/Edit", new 
        { 
            id = result.DashboardId,
            auth_callback = "true",
            access_token = result.AccessToken
        });
    }
}
