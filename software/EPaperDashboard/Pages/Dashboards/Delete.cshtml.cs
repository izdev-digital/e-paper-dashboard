using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LiteDB;
using CSharpFunctionalExtensions;
using EPaperDashboard.Services;

namespace EPaperDashboard.Pages.Dashboards;

public class DeleteModel(DashboardService dashboardService, UserService userService) : PageModel
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
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
        if (dashboard is null)
        {
            ErrorMessage = "Dashboard not found.";
            return Page();
        }
        Name = dashboard.Name;
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
        var dashboard = _dashboardService.GetDashboardsForUser(user.Value.Id).FirstOrDefault(d => d.Id == id.Value);
        if (dashboard is null)
        {
            ErrorMessage = "Dashboard not found.";
            return Page();
        }
        _dashboardService.DeleteDashboard(id.Value);
        return RedirectToPage("/Dashboards");
    }

    private static Result<ObjectId> TryParseObjectId(string id) => Result.Try(
        () => new ObjectId(id),
        _ => "Invalid ObjectId format.");
}
