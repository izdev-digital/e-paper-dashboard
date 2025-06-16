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
    public string NewUsername { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostChangeNicknameAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            ErrorMessage = "New nickname cannot be empty.";
            return Page();
        }
        if (_userService.ChangeUsername(CurrentUsername, NewUsername))
        {
            // Re-sign in with new username
            var user = _userService.GetUserByUsername(NewUsername);
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("IsSuperUser", user.IsSuperUser.ToString().ToLower())
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            }
            SuccessMessage = "Nickname changed successfully.";
        }
        else
        {
            ErrorMessage = "Nickname is already taken or change failed.";
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
}
