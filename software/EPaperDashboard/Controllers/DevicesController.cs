using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Guards;
using EPaperDashboard.Models;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController(DeviceService deviceService, DashboardService dashboardService) : BaseApiController
{
    private readonly DeviceService _deviceService = deviceService;
    private readonly DashboardService _dashboardService = dashboardService;

    [HttpGet]
    public IActionResult GetDevices()
    {
        var devices = _deviceService.GetDevicesForUser(CurrentUserId);
        var result = devices.Select(d => DeviceResponseDto.FromDevice(d)).ToList();
        return Ok(result);
    }

    [HttpGet("dashboard/{dashboardId}")]
    [DashboardOwner]
    public IActionResult GetDevicesForDashboard(string dashboardId)
    {
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return BadRequest("Invalid dashboard ID");
        }

        var devices = _deviceService.GetDevicesForDashboard(id);
        var result = devices.Select(d => DeviceResponseDto.FromDevice(d)).ToList();
        return Ok(result);
    }

    [HttpPut("{deviceId}")]
    public IActionResult UpdateDevice(string deviceId, [FromBody] UpdateDeviceRequest request)
    {
        if (!DeviceId.TryParse(deviceId, out var id))
        {
            return BadRequest("Invalid device ID");
        }

        var device = _deviceService.GetDeviceById(id);
        if (device.HasNoValue)
        {
            return NotFound("Device not found");
        }

        if (device.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        if (request.Name != null)
        {
            device.Value.Name = request.Name;
        }

        if (request.DashboardId != null)
        {
            if (string.IsNullOrEmpty(request.DashboardId))
            {
                // Unassign dashboard
                device.Value.DashboardId = DashboardId.Empty;
            }
            else if (DashboardId.TryParse(request.DashboardId, out var dashboardId))
            {
                // Verify the user owns the dashboard
                var dashboard = _dashboardService.GetDashboardById(dashboardId);
                if (dashboard.HasNoValue)
                {
                    return NotFound("Dashboard not found");
                }
                if (dashboard.Value.UserId != CurrentUserId)
                {
                    return Forbid();
                }
                device.Value.DashboardId = dashboardId;
            }
            else
            {
                return BadRequest("Invalid dashboard ID");
            }
        }

        _deviceService.UpdateDevice(device.Value);
        return Ok(DeviceResponseDto.FromDevice(device.Value));
    }

    [HttpDelete("{deviceId}")]
    public IActionResult DeleteDevice(string deviceId)
    {
        if (!DeviceId.TryParse(deviceId, out var id))
        {
            return BadRequest("Invalid device ID");
        }

        var device = _deviceService.GetDeviceById(id);
        if (device.HasNoValue)
        {
            return NotFound("Device not found");
        }

        if (device.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        _deviceService.DeleteDevice(id);
        return Ok();
    }
}

public record UpdateDeviceRequest(string? Name, string? DashboardId);

public record DeviceResponseDto(
    string Id,
    string DeviceIdentifier,
    string Name,
    string? DashboardId,
    string? DashboardName,
    DateTimeOffset PairedAt,
    DateTimeOffset? LastSeenAt,
    string? FirmwareVersion)
{
    public static DeviceResponseDto FromDevice(Device device, string? dashboardName = null) => new(
        Id: device.Id.ToString(),
        DeviceIdentifier: device.DeviceIdentifier,
        Name: device.Name,
        DashboardId: device.DashboardId == Models.DashboardId.Empty ? null : device.DashboardId.ToString(),
        DashboardName: dashboardName,
        PairedAt: device.PairedAt,
        LastSeenAt: device.LastSeenAt,
        FirmwareVersion: device.FirmwareVersion
    );
}
