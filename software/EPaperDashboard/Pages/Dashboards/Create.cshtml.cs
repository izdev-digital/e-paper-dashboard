using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EPaperDashboard.Pages.Dashboards;

public class CreateModel : PageModel
{
    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string ApiKey { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ApiKey))
        {
            ModelState.AddModelError(string.Empty, "Name and API Key are required.");
            return Page();
        }
        // TODO: Save the new dashboard (Name, Description, ApiKey) to storage
        return RedirectToPage("/Dashboards");
    }
}
