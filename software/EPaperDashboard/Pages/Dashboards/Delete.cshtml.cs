using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Pages.Dashboards;

public class DeleteModel : PageModel
{
    private readonly DashboardService _dashboardService;
    private readonly UserService _userService;

    public DeleteModel(DashboardService dashboardService, UserService userService)
    {
        _dashboardService = dashboardService;
        _userService = userService;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
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
        var dashboard = _dashboardService.GetDashboardsForUser(user.Id).FirstOrDefault(d => d.Id == objectId);
        if (dashboard == null)
        {
            ErrorMessage = "Dashboard not found.";
            return Page();
        }
        _dashboardService.DeleteDashboard(objectId);
        return RedirectToPage("/Dashboards");
    }
}
