using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;
using EPaperDashboard.Data;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace EPaperDashboard.Pages;

public class DashboardsModel : PageModel
{
    private readonly DashboardService _dashboardService;
    private readonly UserService _userService;

    public DashboardsModel(DashboardService dashboardService, UserService userService)
    {
        _dashboardService = dashboardService;
        _userService = userService;
    }

    public List<Dashboard> Dashboards { get; set; } = new();

    public void OnGet()
    {
        var user = _userService.GetUserByUsername(User.Identity?.Name ?? string.Empty);
        if (user != null)
        {
            Dashboards = _dashboardService.GetDashboardsForUser(user.Id);
        }
    }
}
