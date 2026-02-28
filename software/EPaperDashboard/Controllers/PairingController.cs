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
    DeviceService deviceService) : BaseApiController
{
    private readonly PairingService _pairingService = pairingService;
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

    [HttpPost("register")]
    [AllowAnonymous]
    [DeviceAccessible(RequireActivePairing = true)]
    public IActionResult RegisterDevice([FromBody] RegisterDeviceRequest request)
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

        if (session.Value.FailedAttempts >= PairingService.MaxFailedAttempts)
        {
            return StatusCode(429, "Too many failed attempts. Start a new pairing session.");
        }

        if (session.Value.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest("Pairing code expired");
        }

        if (session.Value.Status != PairingStatus.Pending)
        {
            return BadRequest("Pairing session is not in a valid state");
        }

        var registered = _pairingService.RegisterDevice(
            request.Code, request.DeviceIdentifier, request.ScreenWidth, request.ScreenHeight);

        if (registered.HasNoValue)
        {
            return StatusCode(500, "Failed to register device");
        }

        var existingDevice = _deviceService.GetDeviceByIdentifier(registered.Value.DeviceIdentifier!);
        if (existingDevice.HasValue)
        {
            if (existingDevice.Value.UserId != registered.Value.UserId)
            {
                return Conflict("This device is already paired with a different user. It must be removed from that account first.");
            }

            existingDevice.Value.ApiKey = registered.Value.ApiKey;
            existingDevice.Value.PairedAt = DateTimeOffset.UtcNow;
            existingDevice.Value.ScreenWidth = registered.Value.ScreenWidth;
            existingDevice.Value.ScreenHeight = registered.Value.ScreenHeight;
            _deviceService.UpdateDevice(existingDevice.Value);
        }
        else
        {
            var device = new Models.Device
            {
                UserId = registered.Value.UserId,
                DeviceIdentifier = registered.Value.DeviceIdentifier!,
                Name = request.DeviceName ?? registered.Value.DeviceIdentifier!,
                ApiKey = registered.Value.ApiKey,
                PairedAt = DateTimeOffset.UtcNow,
                ScreenWidth = registered.Value.ScreenWidth,
                ScreenHeight = registered.Value.ScreenHeight
            };
            _deviceService.AddDevice(device);
        }

        return Ok(new RegisterDeviceResponse
        {
            ApiKey = registered.Value.ApiKey
        });
    }

    [HttpGet("status")]
    [Authorize]
    public IActionResult GetPairingStatus([FromQuery] string code)
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

        if (session.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        var statusString = session.Value.Status switch
        {
            PairingStatus.Pending => "pending",
            PairingStatus.Completed => "completed",
            _ => "unknown"
        };

        return Ok(new PairingStatusResponse
        {
            Status = statusString,
            DeviceIdentifier = session.Value.DeviceIdentifier
        });
    }
}

public record StartPairingResponse
{
    public string Code { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}

public record RegisterDeviceRequest(string Code, string DeviceIdentifier, string? DeviceName, int? ScreenWidth, int? ScreenHeight);

public record RegisterDeviceResponse
{
    public string ApiKey { get; init; } = string.Empty;
}

public record PairingStatusResponse
{
    public string Status { get; init; } = string.Empty;
    public string? DeviceIdentifier { get; init; }
}
