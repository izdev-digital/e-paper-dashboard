using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;
using LiteDB;
using EPaperDashboard.Services;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Pages.Users;

public class DeleteModel(UserService userService, DashboardService dashboardService) : PageModel
{
    private readonly UserService _userService = userService;

    [BindProperty]
    public string UserId { get; set; } = string.Empty;
    public User? UserToDelete { get; set; }

    public IActionResult OnGet(string id)
    {
        var user = _userService.GetUserById(new ObjectId(id));
        if (user.HasNoValue)
        {
            return RedirectToPage("/Users/Manage");
        }

        UserId = id;
        UserToDelete = user.Value;
        return Page();
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return RedirectToPage("/Users/Manage");
        }

        var id = new ObjectId(UserId);
        var user = _userService.GetUserById(id);
        if (user.HasNoValue)
        {
            return RedirectToPage("/Users/Manage");
        }

        _userService.TryDeleteUser(id);
        return RedirectToPage("/Users/Manage");
    }
}
