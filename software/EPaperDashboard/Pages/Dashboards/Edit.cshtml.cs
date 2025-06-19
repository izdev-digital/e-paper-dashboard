using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LiteDB;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;
using EPaperDashboard.Services;

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
        AccessToken = dashboard.AccessToken;
        Host = dashboard.Host;
        Path = dashboard.Path;
        UpdateTimes = dashboard.UpdateTimes;
        UpdateTimesRaw = UpdateTimes != null ? string.Join(",", UpdateTimes.Select(t => t.ToString("HH:mm"))) : string.Empty;
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
        dashboard.AccessToken = AccessToken;
        dashboard.Host = Host;
        dashboard.Path = Path;

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
                ErrorMessage = "Invalid time format in update times. Use HH:mm, e.g. 06:00,12:00,18:00";
                return Page();
            }
        }
        dashboard.UpdateTimes = UpdateTimes;
        // ApiKey is readonly
        _dashboardService.UpdateDashboard(dashboard);
        return RedirectToPage("/Dashboards");
    }

    private static Result<ObjectId> TryParseObjectId(string id) => Result.Try(
        () => new ObjectId(id),
        _ => "Invalid ObjectId format.");
}
