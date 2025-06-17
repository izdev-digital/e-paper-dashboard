using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace EPaperDashboard.Pages.Users;

public class ProfileModel : PageModel
{
    private readonly UserService _userService;
    public ProfileModel(UserService userService)
    {
        _userService = userService;
    }

    public string CurrentUsername => User.Identity?.Name ?? string.Empty;

    [BindProperty]
    public string NewNickname { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostChangeNicknameAsync()
    {
        if (_userService.ChangeNickname(CurrentUsername, NewNickname))
        {
            SuccessMessage = string.IsNullOrWhiteSpace(NewNickname) ? "Nickname cleared." : "Nickname changed successfully.";
        }
        else
        {
            ErrorMessage = "Nickname change failed.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteProfileAsync()
    {
        if (_userService.DeleteUserByUsername(CurrentUsername))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Index");
        }
        ErrorMessage = "Failed to delete profile.";
        return Page();
    }

    public string GetUsername()
    {
        var user = _userService.GetUserByUsername(CurrentUsername);
        return user?.Username ?? string.Empty;
    }

    public string GetNickname()
    {
        var user = _userService.GetUserByUsername(CurrentUsername);
        return user?.Nickname ?? string.Empty;
    }
}
