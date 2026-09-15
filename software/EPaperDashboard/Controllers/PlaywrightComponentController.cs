using EPaperDashboard.Services.Components.Playwright;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/system/components/playwright")]
[Authorize]
public sealed class PlaywrightComponentController(PlaywrightComponentManager componentManager) : ControllerBase
{
    [HttpGet]
    public ActionResult<PlaywrightComponentStatus> GetStatus() => Ok(componentManager.GetStatus());

    [HttpPost("install")]
    [Authorize(Policy = "SuperUserOnly")]
    public IActionResult Install()
    {
        try
        {
            return componentManager.StartInstallation()
                ? Accepted(componentManager.GetStatus())
                : Conflict(new { message = "The Playwright component is already being installed." });
        }
        catch (PlatformNotSupportedException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete]
    [Authorize(Policy = "SuperUserOnly")]
    public async Task<IActionResult> Uninstall()
    {
        try
        {
            await componentManager.UninstallAsync();
            return Ok(componentManager.GetStatus());
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
