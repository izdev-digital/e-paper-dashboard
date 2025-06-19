using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;
using EPaperDashboard.Services;

namespace EPaperDashboard.Pages.Dashboards;

public class CreateModel(DashboardService dashboardService, UserService userService) : PageModel
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? AccessToken { get; set; }
    [BindProperty]
    public string? Host { get; set; }
    [BindProperty]
    public string? Path { get; set; }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(string.Empty, "Name is required.");
            return Page();
        }
        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user.HasNoValue)
        {
            ModelState.AddModelError(string.Empty, "User not found.");
            return Page();
        }
        // Generate API key automatically
        var apiKey = GenerateApiKey();
        var dashboard = new Dashboard
        {
            Name = Name,
            Description = Description ?? string.Empty,
            ApiKey = apiKey,
            UserId = user.Value.Id
        };
        _dashboardService.AddDashboard(dashboard);
        // Redirect to edit page for the new dashboard
        return RedirectToPage("/Dashboards/Edit", new { id = dashboard.Id.ToString() });
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
