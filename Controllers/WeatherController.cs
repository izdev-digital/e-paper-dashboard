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
            { } => Ok(ConvertToDto(result.Value)),
            _ => NotFound()
        };
    }

    private static WeatherInfoDto ConvertToDto(WeatherInfo value)
    {
        var daily = new DailyWeatherConditionDto(value.Daily.WeatherCode, value.Daily.TemperatureMin, value.Daily.TemperatureMax);
        var hourly = value.Hourly.Select(x => new WeatherConditionDto(x.Time, x.WeatherCode, x.Temperature));
        return new WeatherInfoDto(value.Location, daily, hourly.ToArray());
    }
}

public record WeatherInfoDto(
    string Location,
    DailyWeatherConditionDto Daily,
    WeatherConditionDto[] Hourly);

public record WeatherConditionDto(
    DateTime Time,
    int WeatherCode,
    float Temperature);

public record DailyWeatherConditionDto(
    int WeatherCode,
    float TemperatureMin,
    float TemperatureMax);