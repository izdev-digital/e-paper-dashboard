using FluentResults;

namespace EPaperDashboard.Services;

public interface IWeatherService
{
    Task<Result<WeatherInfo>> GetAsync(string location);
}
