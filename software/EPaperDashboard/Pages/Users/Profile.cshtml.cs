using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using CSharpFunctionalExtensions;
using EPaperDashboard.Services;
using LiteDB;

namespace EPaperDashboard.Pages.Users;

public sealed class ProfileModel(UserService userService) : PageModel
{
    private readonly UserService _userService = userService;

    public string CurrentUsername => User.Identity?.Name ?? string.Empty;
    public string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [BindProperty]
    public string NewNickname { get; set; } = string.Empty;

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public IActionResult OnPostChangeNickname()
    {
        if (_userService.TryChangeNickname(CurrentUsername, NewNickname))
        {
            SuccessMessage = string.IsNullOrWhiteSpace(NewNickname) ? "Nickname cleared." : "Nickname changed successfully.";
        }
        else
        {
            ErrorMessage = "Nickname change failed.";
        }
        return Page();
    }

    public IActionResult OnPostChangePassword()
    {
        var userId = new ObjectId(UserId);
        string[] requiredFields = [CurrentPassword, NewPassword, ConfirmNewPassword];
        if (requiredFields.Any(string.IsNullOrWhiteSpace))
        {
            ErrorMessage = "All password fields are required.";
            return Page();
        }

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "New password and confirmation do not match.";
            return Page();
        }

        if (_userService.TryChangePassword(userId, CurrentPassword, NewPassword))
        {
            SuccessMessage = "Password changed successfully.";
            return RedirectToPage("/Logout");
        }
        else
        {
            ErrorMessage = "Password change failed.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteProfileAsync()
    {
        if (_userService.TryDeleteUserByUsername(CurrentUsername))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Index");
        }
        ErrorMessage = "Failed to delete profile.";
        return Page();
    }

    public string GetUsername() => _userService
        .GetUserByUsername(CurrentUsername)
        .Select(u => u.Username)
        .GetValueOrDefault(string.Empty);

    public string GetNickname() => _userService
        .GetUserByUsername(CurrentUsername)
        .Select(u => u.Nickname ?? string.Empty)
        .GetValueOrDefault(string.Empty);
}
