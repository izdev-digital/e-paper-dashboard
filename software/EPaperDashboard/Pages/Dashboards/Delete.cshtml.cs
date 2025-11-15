using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LiteDB;
using CSharpFunctionalExtensions;
using EPaperDashboard.Services;
using System.Security.Claims;

namespace EPaperDashboard.Pages.Dashboards;

public class DeleteModel(DashboardService dashboardService, UserService userService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ErrorMessage { get; set; }
    private ObjectId UserId => new(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

    public IActionResult OnGet()
    {
        var id = TryParseObjectId(Id);
        if (id.IsFailure)
        {
            ErrorMessage = id.Error;
            return Page();
        }

        var user = userService.GetUserById(UserId);
        if (user.HasNoValue)
        {
            ErrorMessage = "User not found.";
            return Page();
        }

        var dashboard = dashboardService.GetDashboardsForUser(user.Value.Id).FirstOrDefault(d => d.Id == id.Value);
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

        var user = userService.GetUserById(UserId);
        if (user.HasNoValue)
        {
            ErrorMessage = "User not found.";
            return Page();
        }

        var dashboard = dashboardService.GetDashboardsForUser(user.Value.Id).FirstOrDefault(d => d.Id == id.Value);
        if (dashboard is null)
        {
            ErrorMessage = "Dashboard not found.";
            return Page();
        }

        dashboardService.DeleteDashboard(id.Value);
        return RedirectToPage("/Dashboards");
    }

    private static Result<ObjectId> TryParseObjectId(string id) => Result.Try(
        () => new ObjectId(id),
        _ => "Invalid ObjectId format.");
}
