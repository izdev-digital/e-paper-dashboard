using Microsoft.AspNetCore.Mvc.RazorPages;
using EPaperDashboard.Models;

namespace EPaperDashboard.Pages;

public class DashboardsModel : PageModel
{
    public List<Dashboard> Dashboards { get; set; } = new List<Dashboard>
    {
        new Dashboard { Id = 1, Name = "Home", Description = "Main dashboard for home." },
        new Dashboard { Id = 2, Name = "Office", Description = "Office dashboard." }
    };

    public void OnGet()
    {
    }
}
