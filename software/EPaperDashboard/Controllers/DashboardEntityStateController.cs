using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/entity-states")]
[Authorize]
[DashboardOwner]
public class DashboardEntityStateController(
    IEntityStateProvider entityStateProvider) : ControllerBase
{
    private readonly IEntityStateProvider _entityStateProvider = entityStateProvider;

    [HttpPost]
    public async Task<IActionResult> GetEntityStates(string dashboardId, [FromBody] EntityIdsRequest request)
    {
        var result = await _entityStateProvider.FetchEntityStatesAsync(dashboardId, request.EntityIds ?? []);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    public record EntityIdsRequest(string[] EntityIds);
}
