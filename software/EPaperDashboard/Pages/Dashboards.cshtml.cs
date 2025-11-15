using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;
using CSharpFunctionalExtensions;
using EPaperDashboard.Services;
using LiteDB;
using System.Security.Claims;

namespace EPaperDashboard.Pages;

public sealed class DashboardsModel(DashboardService dashboardService, UserService userService) : PageModel
{
    public List<Dashboard> Dashboards { get; set; } = [];

    private ObjectId UserId => new(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

    public void OnGet() => Dashboards = userService
        .GetUserById(UserId)
        .Select(user => dashboardService.GetDashboardsForUser(user.Id))
        .GetValueOrDefault([]);
}
