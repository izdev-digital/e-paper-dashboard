using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Pages.Dashboards;

public class EditModel : PageModel
{
    private readonly DashboardService _dashboardService;
    private readonly UserService _userService;

    public EditModel(DashboardService dashboardService, UserService userService)
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
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        ObjectId objectId;
        try
        {
            objectId = new ObjectId(Id);
        }
        catch
        {
            ErrorMessage = "Invalid dashboard id.";
            return Page();
        }
        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return Page();
        }
        var dashboard = _dashboardService.GetDashboardsForUser(user.Id).FirstOrDefault(d => d.Id == objectId);
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
        ObjectId objectId;
        try
        {
            objectId = new ObjectId(Id);
        }
        catch
        {
            ErrorMessage = "Invalid dashboard id.";
            return Page();
        }
        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return Page();
        }
        var dashboards = _dashboardService.GetDashboardsForUser(user.Id);
        var dashboard = dashboards.FirstOrDefault(d => d.Id == objectId);
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
}
