using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using EPaperDashboard.Models;

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
    [BindProperty]
    public string? UpdateTimesRaw { get; set; }
    [BindProperty]
    public List<TimeOnly>? UpdateTimes { get; set; }

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
        if (!string.IsNullOrWhiteSpace(UpdateTimesRaw))
        {
            try
            {
                UpdateTimes = UpdateTimesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => TimeOnly.ParseExact(t, "HH:mm"))
                    .ToList();
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Invalid time format in update times. Use HH:mm, e.g. 06:00,12:00,18:00");
                return Page();
            }
        }
        var dashboard = new Dashboard
        {
            Name = Name,
            Description = Description ?? string.Empty,
            ApiKey = apiKey,
            UserId = user.Value.Id,
            AccessToken = AccessToken,
            Host = Host,
            Path = Path,
            UpdateTimes = UpdateTimes
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
