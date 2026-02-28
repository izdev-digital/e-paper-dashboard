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
            ConfirmationPin = session.ConfirmationPin,
            ExpiresAt = session.ExpiresAt
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
            return BadRequest("Pairing session is not in a valid state for this operation");
        }

        _pairingService.SetAwaitingConfirmation(session.Value.Id, request.DeviceIdentifier, request.ScreenWidth, request.ScreenHeight);

        return Ok(new CompletePairingResponse
        {
            ConfirmationPin = session.Value.ConfirmationPin
        });
    }

    [HttpPost("confirm")]
    [Authorize]
    public IActionResult ConfirmPairing([FromBody] ConfirmPairingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Code is required");
        }

        var session = _pairingService.GetPairingSessionByCode(request.Code);
        if (session.HasNoValue)
        {
            return NotFound("Invalid pairing code");
        }

        if (session.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        if (session.Value.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest("Pairing code expired");
        }

        if (session.Value.Status != PairingStatus.AwaitingConfirmation)
        {
            return BadRequest("Pairing session is not awaiting confirmation");
        }

        if (string.IsNullOrWhiteSpace(session.Value.DeviceIdentifier))
        {
            return BadRequest("No device has submitted this pairing code yet");
        }

        _pairingService.ConfirmPairingSession(session.Value.Id);

        var confirmed = _pairingService.GetPairingSessionById(session.Value.Id);
        if (confirmed.HasNoValue)
        {
            return StatusCode(500, "Failed to confirm pairing session");
        }

        var existingDevice = _deviceService.GetDeviceByIdentifier(confirmed.Value.DeviceIdentifier!);
        if (existingDevice.HasValue)
        {
            if (existingDevice.Value.UserId != confirmed.Value.UserId)
            {
                return Conflict("This device is already paired with a different user. It must be removed from that account first.");
            }

            existingDevice.Value.ApiKey = confirmed.Value.ApiKey;
            existingDevice.Value.PairedAt = DateTimeOffset.UtcNow;
            existingDevice.Value.ScreenWidth = confirmed.Value.ScreenWidth;
            existingDevice.Value.ScreenHeight = confirmed.Value.ScreenHeight;
            _deviceService.UpdateDevice(existingDevice.Value);
        }
        else
        {
            var device = new Models.Device
            {
                UserId = confirmed.Value.UserId,
                DeviceIdentifier = confirmed.Value.DeviceIdentifier!,
                Name = confirmed.Value.DeviceIdentifier!,
                ApiKey = confirmed.Value.ApiKey,
                PairedAt = DateTimeOffset.UtcNow,
                ScreenWidth = confirmed.Value.ScreenWidth,
                ScreenHeight = confirmed.Value.ScreenHeight
            };
            _deviceService.AddDevice(device);
        }

        return Ok();
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
            _pairingService.IncrementFailedAttempts(session.Value.Id);
            return NotFound("Invalid pairing code");
        }

        if (session.Value.FailedAttempts >= PairingService.MaxFailedAttempts)
        {
            return StatusCode(429, "Too many failed attempts");
        }

        if (session.Value.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest("Pairing code expired");
        }

        if (session.Value.IsCompleted)
        {
            return BadRequest("Pairing already completed");
        }

        if (session.Value.Status == PairingStatus.Confirmed)
        {
            _pairingService.CompletePairingSession(session.Value.Id);

            return Ok(new PollPairingResponse
            {
                Status = "paired",
                ApiKey = session.Value.ApiKey
            });
        }

        var statusString = session.Value.Status switch
        {
            PairingStatus.Pending => "pending",
            PairingStatus.AwaitingConfirmation => "awaiting_confirmation",
            _ => "pending"
        };

        return Ok(new PollPairingResponse
        {
            Status = statusString
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
            PairingStatus.AwaitingConfirmation => "awaiting_confirmation",
            PairingStatus.Confirmed => "confirmed",
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
    public string ConfirmationPin { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}

public record PollPairingResponse
{
    public string Status { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKey { get; init; }
}

public record CompletePairingResponse
{
    public string ConfirmationPin { get; init; } = string.Empty;
}

public record CompletePairingRequest(string Code, string DeviceIdentifier, string? DeviceName, int? ScreenWidth, int? ScreenHeight);

public record ConfirmPairingRequest(string Code);

public record PairingStatusResponse
{
    public string Status { get; init; } = string.Empty;
    public string? DeviceIdentifier { get; init; }
}
