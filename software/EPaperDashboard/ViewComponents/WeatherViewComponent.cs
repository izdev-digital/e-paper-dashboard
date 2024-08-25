using EPaperDashboard.Services.Weather;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.ViewComponents;

public class WeatherViewComponent : ViewComponent
{
    private readonly IWeatherService _weatherService;

    public WeatherViewComponent(IWeatherService weatherService) =>
        _weatherService = weatherService;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var weatherInfo = await _weatherService.GetAsync("Berlin");
        return weatherInfo.IsFailed
            ? View(null)
            : View(weatherInfo.Value);
    }
}
