using Microsoft.AspNetCore.Mvc;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/app")]
public class AppApiController : ControllerBase
{
    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(new { version = Constants.AppVersion });
    }
}
