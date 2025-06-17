using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using LiteDB;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Pages.Dashboards;

public class EditModel(DashboardService dashboardService, UserService userService) : PageModel
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string ApiKey { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        var id = TryParseObjectId(Id);
        if (id.IsFailure)
        {
            ErrorMessage = id.Error;
            return Page();
        }

        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user.HasNoValue)
        {
            ErrorMessage = "User not found.";
            return Page();
        }
        var dashboard = _dashboardService.GetDashboardsForUser(user.Value.Id).FirstOrDefault(d => d.Id == id.Value);
        if (dashboard == null)
        {
            ErrorMessage = "Dashboard not found.";
            return Page();
        }
        Name = dashboard.Name;
        Description = dashboard.Description;
        ApiKey = dashboard.ApiKey;
        return Page();
    }

    public IActionResult OnPost()
    {
        var id = TryParseObjectId(Id);
        if (id.IsFailure)
        {
            ErrorMessage = id.Error;
            return Page();
        }

        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user.HasNoValue)
        {
            ErrorMessage = "User not found.";
            return Page();
        }
        var dashboards = _dashboardService.GetDashboardsForUser(user.Value.Id);
        var dashboard = dashboards.FirstOrDefault(d => d.Id == id.Value);
        if (dashboard == null)
        {
            ErrorMessage = "Dashboard not found.";
            return Page();
        }
        dashboard.Name = Name;
        dashboard.Description = Description ?? string.Empty;
        // ApiKey is readonly
        _dashboardService.UpdateDashboard(dashboard);
        return RedirectToPage("/Dashboards");
    }

    private static Result<ObjectId> TryParseObjectId(string id) => Result.Try(
        () => new ObjectId(id),
        _ => "Invalid ObjectId format.");
}
