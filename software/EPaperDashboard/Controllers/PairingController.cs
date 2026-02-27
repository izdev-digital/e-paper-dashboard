using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Guards;
using EPaperDashboard.Models;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/pairing")]
public class PairingController(
    PairingService pairingService,
    DashboardService dashboardService,
    DeviceService deviceService) : BaseApiController
{
    private readonly PairingService _pairingService = pairingService;
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly DeviceService _deviceService = deviceService;

    [HttpPost("start")]
    [Authorize]
    public IActionResult StartPairing()
    {
        var session = _pairingService.CreatePairingSession(CurrentUserId);

        return Ok(new StartPairingResponse
        {
            Code = session.Code,
            ExpiresAt = session.ExpiresAt
        });
    }

    [HttpGet("poll")]
    [AllowAnonymous]
    [DeviceAccessible(RequireActivePairing = true)]
    public IActionResult PollPairing([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("Code is required");
        }

        var session = _pairingService.GetPairingSessionByCode(code);
        if (session.HasNoValue)
        {
            return NotFound("Invalid pairing code");
        }

        if (session.Value.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest("Pairing code expired");
        }

        if (session.Value.IsCompleted)
        {
            return BadRequest("Pairing already completed");
        }

        return Ok(new PollPairingResponse
        {
            ApiKey = session.Value.ApiKey
        });
    }

    [HttpPost("complete")]
    [AllowAnonymous]
    [DeviceAccessible(RequireActivePairing = true)]
    public IActionResult CompletePairing([FromBody] CompletePairingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.DeviceIdentifier))
        {
            return BadRequest("Code and DeviceIdentifier are required");
        }

        var session = _pairingService.GetPairingSessionByCode(request.Code);
        if (session.HasNoValue)
        {
            return NotFound("Invalid pairing code");
        }

        if (session.Value.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest("Pairing code expired");
        }

        if (session.Value.IsCompleted)
        {
            return BadRequest("Pairing already completed");
        }

        var existingDevice = _deviceService.GetDeviceByIdentifier(request.DeviceIdentifier);
        if (existingDevice.HasValue)
        {
            existingDevice.Value.UserId = session.Value.UserId;
            existingDevice.Value.ApiKey = session.Value.ApiKey;
            existingDevice.Value.PairedAt = DateTimeOffset.UtcNow;
            existingDevice.Value.Name = request.DeviceName ?? request.DeviceIdentifier;
            _deviceService.UpdateDevice(existingDevice.Value);
        }
        else
        {
            var device = new Models.Device
            {
                UserId = session.Value.UserId,
                DeviceIdentifier = request.DeviceIdentifier,
                Name = request.DeviceName ?? request.DeviceIdentifier,
                ApiKey = session.Value.ApiKey,
                PairedAt = DateTimeOffset.UtcNow
            };
            _deviceService.AddDevice(device);
        }

        _pairingService.CompletePairingSession(session.Value.Id, request.DeviceIdentifier);

        return Ok();
    }
}

public record StartPairingResponse
{
    public string Code { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}

public record PollPairingResponse
{
    public string ApiKey { get; init; } = string.Empty;
}

public record CompletePairingRequest(string Code, string DeviceIdentifier, string? DeviceName);
