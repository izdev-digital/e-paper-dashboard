using System.Globalization;
using EPaperDashboard.Guards;
using EPaperDashboard.Models.Weather;
using EPaperDashboard.Services.Weather.Dto;
using EPaperDashboard.Utilities;
using FluentResults;
using FluentResults.Extensions;
using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather;

public sealed class WeatherService : IWeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocationService _locationService;

    public WeatherService(IHttpClientFactory httpClientFactory, ILocationService locationService)
    {
        _httpClientFactory = httpClientFactory;
        _locationService = locationService;
    }

    public async Task<Result<WeatherInfo>> GetAsync(string location) =>
        await _locationService
            .GetLocationDetailsAsync(location)
            .Bind(GetWeatherInformationAsync);

    private async Task<Result<WeatherInfo>> GetWeatherInformationAsync(Location location)
    {
        var client = _httpClientFactory.CreateClient();
        var uri = UriUtilities.CreateUri(new Uri("https://api.open-meteo.com"), "v1/forecast", new Dictionary<string, string>{
                    {"latitude", location.Latitude.ToString(CultureInfo.InvariantCulture)},
                    {"longitude",location.Longitude.ToString(CultureInfo.InvariantCulture)},
                    {"timezone",location.TimeZone ?? "GMT"},
                    {"daily","weather_code,apparent_temperature_min,apparent_temperature_max"},
                    {"forecast_days","1"},
                    {"hourly","apparent_temperature,weather_code"}});
        var response = await client.GetAsync(uri);
        var json = await response.Content.ReadAsStringAsync();

        return Convert(json, location.Name);
    }

    private static Result<WeatherInfo> Convert(string? json, string location)
    {
        var weatherInformationDto = JsonConvert.DeserializeObject<WeatherInformationDto>(Guard.NeitherNullNorWhitespace(json, nameof(json)));
        if (weatherInformationDto is null)
        {
            return Result.Fail("Failed to convert the weather information json");
        }

        if (weatherInformationDto.DailyInformation is null)
        {
            return Result.Fail("No daily information is provided");
        }

        if (weatherInformationDto.HourlyInformation is null)
        {
            return Result.Fail("No hourly information is provided");
        }

        if (weatherInformationDto.DailyInformationUnits is null)
        {
            return Result.Fail("No daily information units is provided");
        }

        if (weatherInformationDto.HourlyInformationUnits is null)
        {
            return Result.Fail("No hourly information units is provided");
        }

        var hourlyConditions = Enumerable
            .Zip(
                weatherInformationDto.HourlyInformation.Time,
                weatherInformationDto.HourlyInformation.ApparentTemperature,
                weatherInformationDto.HourlyInformation.WeatherCode)
            .Select(item => new WeatherCondition(
                item.First,
                item.Third,
                new Temperature(
                    item.Second,
                    weatherInformationDto.HourlyInformationUnits?.TemperatureUnits ?? string.Empty)))
            .ToArray();

        var dailyConditions = new DailyWeatherCondition(
                weatherInformationDto.DailyInformation.WeatherCode.First(),
                new Temperature(
                    weatherInformationDto.DailyInformation.ApparentTemperatureMin.First(),
                    weatherInformationDto.DailyInformationUnits.TemperatureMinUnits ?? string.Empty),
                new Temperature(
                    weatherInformationDto.DailyInformation.ApparentTemperatureMax.First(),
                    weatherInformationDto.DailyInformationUnits.TemperatureMaxUnits ?? string.Empty));
        
        return Result.Ok(new WeatherInfo(location, dailyConditions, hourlyConditions));
    }
}