using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EPaperDashboard.Pages;

public class LoginModel : PageModel
{
    [BindProperty]
    public string Username { get; set; }

    [BindProperty]
    public string Password { get; set; }

    public IActionResult OnPostLogin()
    {
        // Implement login logic here
        return RedirectToPage("/Home");
    }

    public IActionResult OnPostRegister()
    {
        // Implement registration logic here
        return RedirectToPage("/Home");
    }
}
