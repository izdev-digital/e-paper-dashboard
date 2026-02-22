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

    [HttpGet("dashboard/{dashboardId}")]
    [DashboardOwner]
    public IActionResult GetDevicesForDashboard(string dashboardId)
    {
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return BadRequest("Invalid dashboard ID");
        }

        var devices = _deviceService.GetDevicesForDashboard(id);
        return Ok(devices);
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

        var dashboard = _dashboardService.GetDashboardById(device.Value.DashboardId);
        if (dashboard.HasNoValue)
        {
            return NotFound("Dashboard not found");
        }

        if (dashboard.Value.UserId != CurrentUserId)
        {
            return Forbid();
        }

        _deviceService.DeleteDevice(id);
        return Ok();
    }
}
