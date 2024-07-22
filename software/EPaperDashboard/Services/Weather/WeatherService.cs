using System.Globalization;
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
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientConfigurations.WeatherService);
            var uri = UriUtilities.CreateUri(new Uri("https://api.open-meteo.com"), "v1/forecast", new Dictionary<string, string>{
                    {"latitude", location.Latitude.ToString(CultureInfo.InvariantCulture)},
                    {"longitude",location.Longitude.ToString(CultureInfo.InvariantCulture)},
                    {"timezone",location.TimeZone},
                    {"daily","weather_code,apparent_temperature_min,apparent_temperature_max"},
                    {"forecast_days","1"},
                    {"hourly","apparent_temperature,weather_code"}});
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            var weatherInformationDto = JsonConvert.DeserializeObject<WeatherInformationDto>(json);

            return Convert(weatherInformationDto, location.Name);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error("Failed to fetch weather information").CausedBy(ex));
        }
    }

    private static Result<WeatherInfo> Convert(WeatherInformationDto? weatherInformationDto, string location) => Result
        .FailIf(weatherInformationDto is null, "Failed to convert the weather information json")
        .Bind(() => Result.Merge(
            Result.FailIf(
                weatherInformationDto!.DailyInformation is null,
                "No daily information is provided"),
            Result.FailIf(
                weatherInformationDto.HourlyInformation is null,
                "No hourly information is provided"),
            Result.FailIf(
                weatherInformationDto.DailyInformationUnits is null,
                "No daily information units is provided"),
            Result.FailIf(
                string.IsNullOrWhiteSpace(weatherInformationDto.DailyInformationUnits?.TemperatureMinUnits),
                "No daily min temperature units is provided"),
            Result.FailIf(
                string.IsNullOrWhiteSpace(weatherInformationDto.DailyInformationUnits?.TemperatureMaxUnits),
                "No daily max temperature units is provided"),
            Result.FailIf(
                weatherInformationDto.HourlyInformationUnits is null,
                "No hourly information units is provided"),
            Result.FailIf(
                string.IsNullOrWhiteSpace(weatherInformationDto.HourlyInformationUnits?.TemperatureUnits),
                "No hourly temperature units is provided")))
        .Bind<WeatherInfo>(() => new WeatherInfo(
            location,
            new DailyWeatherCondition(
                weatherInformationDto!.DailyInformation!.WeatherCode.First(),
                new Temperature(
                    weatherInformationDto.DailyInformation.ApparentTemperatureMin.First(),
                    weatherInformationDto.DailyInformationUnits!.TemperatureMinUnits ?? string.Empty),
                new Temperature(
                    weatherInformationDto.DailyInformation.ApparentTemperatureMax.First(),
                    weatherInformationDto.DailyInformationUnits.TemperatureMaxUnits ?? string.Empty)),
            Enumerable.Zip(
                    weatherInformationDto.HourlyInformation!.Time,
                    weatherInformationDto.HourlyInformation.ApparentTemperature,
                    weatherInformationDto.HourlyInformation.WeatherCode)
                .Select(item => new WeatherCondition(
                    item.First,
                    item.Third,
                    new Temperature(
                        item.Second,
                        weatherInformationDto.HourlyInformationUnits!.TemperatureUnits ?? string.Empty)))
                .ToArray()));
}