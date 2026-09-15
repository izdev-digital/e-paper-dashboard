using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Components.Playwright;
using EPaperDashboard.Services.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/system/components/rendering")]
[Authorize]
public sealed class PlaywrightComponentController(
    PlaywrightComponentManager componentManager,
    IPageToImageRenderingService renderingService) : ControllerBase
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
                : Conflict(new { message = "The rendering component is already being installed." });
        }
        catch (PlatformNotSupportedException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("test")]
    [Authorize(Policy = "SuperUserOnly")]
    public async Task<IActionResult> Test()
    {
        var result = await renderingService.RenderHtmlAsync(
            "<!doctype html><html><body style=\"margin:0;background:#fff;color:#000\">izBoard</body></html>",
            new Size(200, 100));
        if (result.IsFailure)
            return StatusCode(500, new { message = result.Error });

        using var image = result.Value;
        return Ok(new { message = "Rendering component tested successfully." });
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
