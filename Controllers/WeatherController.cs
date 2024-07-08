using EPaperDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public WeatherController(IWeatherService weatherService) =>
     _weatherService = weatherService;

    [HttpGet]
    public async Task<ActionResult<WeatherInfoDto>> GetWeatherInfo()
    {
        var result = await _weatherService.GetAsync("Berlin");

        return result switch
        {
            { IsFailed: true } => BadRequest(),
            { } => Ok(result.Value),
            _ => NotFound()
        };
    }
}

public record WeatherInfoDto(
    string Location,
    DailyWeatherConditionDto Daily,
    WeatherConditionDto[] Hourly);

public record WeatherConditionDto(
    DateTime Time,
    int WeatherCode,
    float Temperature,
    string Description);

public record DailyWeatherConditionDto(
    int WeatherCode,
    float TemperatureMin,
    float TemperatureMax,
    string Description);