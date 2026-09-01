using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/todo-items")]
[Authorize]
[DashboardOwner]
public class DashboardTodoController(
    ITodoDataProvider todoDataProvider) : ControllerBase
{
    private readonly ITodoDataProvider _todoDataProvider = todoDataProvider;

    [HttpGet("{todoEntityId}")]
    public async Task<IActionResult> GetTodoItems(string dashboardId, string todoEntityId)
    {
        var result = await _todoDataProvider.FetchTodoItemsAsync(dashboardId, todoEntityId, HttpContext.RequestAborted);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }
}
