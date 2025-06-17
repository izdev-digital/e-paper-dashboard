using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Pages.Dashboards;

public class CreateModel : PageModel
{
    private readonly DashboardService _dashboardService;
    private readonly UserService _userService;

    public CreateModel(DashboardService dashboardService, UserService userService)
    {
        _dashboardService = dashboardService;
        _userService = userService;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(string.Empty, "Name is required.");
            return Page();
        }
        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user == null)
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
            UserId = user.Id
        };
        _dashboardService.AddDashboard(dashboard);
        return RedirectToPage("/Dashboards");
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
