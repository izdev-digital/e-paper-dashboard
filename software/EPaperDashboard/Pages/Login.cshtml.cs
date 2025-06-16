using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EPaperDashboard.Pages;

public class LoginModel(UserService userService) : PageModel
{
    private readonly UserService _userService = userService;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        var user = _userService.GetUserByUsername(Username);
        if (user != null && _userService.IsUserValid(Username, Password))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.Username),
                new("IsSuperUser", user.IsSuperUser.ToString().ToLower())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return RedirectToPage("/Index");
        }
        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return Page();
    }
}
