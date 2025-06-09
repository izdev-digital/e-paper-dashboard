using System.ComponentModel.DataAnnotations;
using System.Web;
using EPaperDashboard.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/hass")]
public sealed class HassController(ILogger<HassController> logger) : ControllerBase
{
    private readonly ILogger<HassController> _logger = logger;

    [HttpGet("auth")]
    public IActionResult GetLoginUrl()
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query.Add("client_id", EnvironmentConfiguration.ClientUri.AbsoluteUri);
        query.Add("redirect_uri", new Uri(EnvironmentConfiguration.ClientUri, "hass/auth_callback").AbsoluteUri);
        var authUri = new Uri(EnvironmentConfiguration.HassUri, $"auth/authorize?{query}");
        return Content($"<a href=\"{authUri}\">Authenticate with Home Assistant</a>", "text/html");
    }

    [HttpGet("auth_callback")]
    public IActionResult AuthCallback([Required][FromQuery] string code)
    {
        _logger.LogInformation("Received auth code: {0}", code);
        return NoContent();
    }
}
