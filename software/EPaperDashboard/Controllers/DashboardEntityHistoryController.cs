using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/entity-history")]
[Authorize]
[DashboardOwner]
public class DashboardEntityHistoryController(
    IEntityHistoryProvider entityHistoryProvider) : ControllerBase
{
    private readonly IEntityHistoryProvider _entityHistoryProvider = entityHistoryProvider;

    [HttpPost]
    public async Task<IActionResult> GetEntityHistory(string dashboardId, [FromBody] EntityHistoryRequest request)
    {
        var hours = Clamp(request.Hours ?? 24, 1, 720);
        var result = await _entityHistoryProvider.FetchEntityHistoryAsync(dashboardId, request.EntityIds ?? [], hours);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    public record EntityHistoryRequest(string[] EntityIds, int? Hours = 24);

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}
