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

    [BindProperty]
    public string ApiKey { get; set; } = string.Empty;

    public void OnGet() { }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ApiKey))
        {
            ModelState.AddModelError(string.Empty, "Name and API Key are required.");
            return Page();
        }
        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "User not found.");
            return Page();
        }
        var dashboard = new Dashboard
        {
            Name = Name,
            Description = Description ?? string.Empty,
            ApiKey = ApiKey,
            UserId = user.Id
        };
        _dashboardService.AddDashboard(dashboard);
        return RedirectToPage("/Dashboards");
    }
}
