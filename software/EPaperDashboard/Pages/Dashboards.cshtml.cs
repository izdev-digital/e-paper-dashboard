using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;
using EPaperDashboard.Data;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Pages;

public sealed class DashboardsModel(DashboardService dashboardService, UserService userService) : PageModel
{
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly UserService _userService = userService;

    public List<Dashboard> Dashboards { get; set; } = [];

    public void OnGet() => Dashboards = _userService
        .GetUserByUsername(User.Identity?.Name ?? string.Empty)
        .Select(user => _dashboardService.GetDashboardsForUser(user.Id))
        .GetValueOrDefault([]);
}
