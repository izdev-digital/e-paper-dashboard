using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using CSharpFunctionalExtensions;
using EPaperDashboard.Services;

namespace EPaperDashboard.Pages.Users;

public sealed class ProfileModel(UserService userService) : PageModel
{
    private readonly UserService _userService = userService;

    public string CurrentUsername => User.Identity?.Name ?? string.Empty;

    [BindProperty]
    public string NewNickname { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public IActionResult OnPostChangeNicknameAsync()
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
