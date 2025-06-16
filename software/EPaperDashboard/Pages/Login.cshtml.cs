using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;

namespace EPaperDashboard.Pages;

public class LoginModel(UserService userService) : PageModel
{
    private readonly UserService _userService = userService;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public IActionResult OnPostLogin()
    {
        if (_userService.IsUserValid(Username, Password))
        {
            // TODO: Set authentication cookie/session
            return RedirectToPage("/Home");
        }
        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return Page();
    }

    public IActionResult OnPostRegister()
    {
        if (_userService.CreateUser(Username, Password))
        {
            // TODO: Set authentication cookie/session
            return RedirectToPage("/Home");
        }
        ModelState.AddModelError(string.Empty, "User already exists.");
        return Page();
    }
}
