using System.Globalization;
using System.Web;
using EPaperDashboard.Guards;
using EPaperDashboard.Models.Weather;
using EPaperDashboard.Services.Weather.Dto;
using FluentResults;
using FluentResults.Extensions;
using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather;

public class WeatherService : IWeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherService(IHttpClientFactory httpClientFactory) =>
     _httpClientFactory = httpClientFactory;

    public async Task<Result<WeatherInfo>> GetAsync(string location)
    {
        try
        {
            Guard.NeitherNullNorWhitespace(location, nameof(location));
            return await GetLocationDetailsAsync(location)
                .Bind(GetWeatherInformationAsync);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error("Failed to get weather information").CausedBy(ex));
        }
    }

    private async Task<Result<WeatherInfo>> GetWeatherInformationAsync(LocationDto location)
    {
        var client = _httpClientFactory.CreateClient();
        var uri = CreateUri(new Uri("https://api.open-meteo.com"), "v1/forecast", new Dictionary<string, string>{
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

    private static Result<WeatherInfo> Convert(string? json, string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return Result.Fail("Locaiton name is not provided");
        }

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

        var weatherConditions = Enumerable
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
        return Result.Ok(new WeatherInfo(location, dailyConditions, weatherConditions));
    }

    private async Task<Result<LocationDto>> GetLocationDetailsAsync(string location)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var uri = CreateUri(new Uri("https://geocoding-api.open-meteo.com"), "v1/search", new Dictionary<string, string>{
                {"name", location},
                {"count", "1"},
                {"format", "json"}
            });
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            var locations = JsonConvert.DeserializeObject<LocationResultsDto>(json);

            if (locations is null)
            {
                return Result.Fail<LocationDto>("Failed to deserialize location object");
            }

            if (!locations.Results.Any())
            {
                return Result.Fail<LocationDto>("Information about location was not found");
            }

            return Result.Ok(locations.Results.First());
        }
        catch (Exception ex)
        {
            return Result.Fail<LocationDto>(new Error("Failed to fetch location information").CausedBy(ex));
        }
    }

    private static Uri CreateUri(Uri baseUri, string path, IReadOnlyDictionary<string, string> queryParameters) => new UriBuilder(baseUri)
    {
        Path = path,
        Query = queryParameters
            .Aggregate(HttpUtility.ParseQueryString(string.Empty), (seed, item) =>
            {
                seed.Add(item.Key, item.Value);
                return seed;
            }).ToString()
    }.Uri;
}