using EPaperDashboard.Models.Weather;
using EPaperDashboard.Services.Weather;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/weather")]
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
            { IsFailed: true } => BadRequest(result.Errors),
            { IsSuccess: true } => Ok(ConvertToDto(result.Value)),
            _ => NotFound()
        };
    }

    private static WeatherInfoDto ConvertToDto(WeatherInfo value)
    {
        var daily = new DailyWeatherConditionDto(value.Daily.WeatherCode, ConvertToDto(value.Daily.TemperatureMin), ConvertToDto(value.Daily.TemperatureMax));
        var hourly = value.Hourly.Select(x => new WeatherConditionDto(x.Time, x.WeatherCode, ConvertToDto(x.Temperature)));
        return new WeatherInfoDto(value.Location, daily, hourly.ToArray());
    }

    private static TemperatureDto ConvertToDto(Temperature temperature) =>
        new(temperature.Value, temperature.Unit);
}

public record TemperatureDto(float Value, string Units);

public record WeatherInfoDto(
    string Location,
    DailyWeatherConditionDto Daily,
    WeatherConditionDto[] Hourly);

public record WeatherConditionDto(
    DateTime Time,
    int WeatherCode,
    TemperatureDto Temperature);

public record DailyWeatherConditionDto(
    int WeatherCode,
    TemperatureDto TemperatureMin,
    TemperatureDto TemperatureMax);