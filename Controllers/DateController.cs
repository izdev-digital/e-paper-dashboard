using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("[controller]")]
public class DateController : ControllerBase
{
    [HttpGet]
    public IActionResult Fetch() => Ok(DateTime.Now);
}
