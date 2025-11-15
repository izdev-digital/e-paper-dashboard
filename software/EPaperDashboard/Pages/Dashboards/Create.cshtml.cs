using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using LiteDB;
using System.Security.Claims;

namespace EPaperDashboard.Pages.Dashboards;

public class CreateModel(DashboardService dashboardService, UserService userService) : PageModel
{
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

    private ObjectId UserId => new(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(string.Empty, "Name is required.");
            return Page();
        }
        var user = userService.GetUserById(UserId);
        if (user.HasNoValue)
        {
            ModelState.AddModelError(string.Empty, "User not found.");
            return Page();
        }
        
        var apiKey = GenerateApiKey();
        var dashboard = new Dashboard
        {
            Name = Name,
            Description = Description ?? string.Empty,
            ApiKey = apiKey,
            UserId = user.Value.Id
        };
        dashboardService.AddDashboard(dashboard);
        
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
