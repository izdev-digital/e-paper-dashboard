using EPaperDashboard.Models.Weather;
using FluentResults;

namespace EPaperDashboard.Services.Weather;

public interface IWeatherService
{
    Task<Result<WeatherInfo>> GetAsync(string location);
}
