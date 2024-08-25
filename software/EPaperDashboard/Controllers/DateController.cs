using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/date")]
public class DateController : ControllerBase
{
    [HttpGet]
    public ActionResult<DateDto> Fetch() => Ok(new DateDto(DateOnly.FromDateTime(DateTime.Now)));
}

public record DateDto(DateOnly CurrentDate);