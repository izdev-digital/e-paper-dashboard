using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Pages.Users;

public sealed class ManageModel(UserService userService) : PageModel
{
    private readonly UserService _userService = userService;

    public List<User> Users { get; set; } = [];

    [BindProperty]
    public string NewUsername { get; set; } = string.Empty;
    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
        Users = _userService.GetAllUsers();
    }

    public IActionResult OnPostAdd()
    {
        if (_userService.TryCreateUser(NewUsername, NewPassword))
        {
            SuccessMessage = "User added successfully.";
        }
        else
        {
            ErrorMessage = "User already exists.";
        }
        Users = _userService.GetAllUsers();
        return Page();
    }

    public IActionResult OnPostDelete(string id)
    {
        try
        {
            var objectId = new ObjectId(id);
            if (_userService.TryDeleteUser(objectId))
            {
                SuccessMessage = "User deleted.";
            }
            else
            {
                ErrorMessage = "Cannot delete user.";
            }
        }
        catch
        {
            ErrorMessage = "Invalid user id.";
        }
        Users = _userService.GetAllUsers();
        return Page();
    }
}
