using System.Collections.Immutable;
using System.Web;
using EPaperDashboard.Guards;
using FluentResults;
using Newtonsoft.Json;

namespace EPaperDashboard.Services;

public class WeatherService : IWeatherService
{
    private readonly Uri _apiBaseUrl = new("https://api.open-meteo.com/v1/");

    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherService(IHttpClientFactory httpClientFactory) =>
     _httpClientFactory = httpClientFactory;

    public async Task<Result<WeatherInfo>> GetAsync(string location)
    {
        try
        {
            Guard.NeitherNullNorWhitespace(location, nameof(location));
            var locationDetails = await GetLocationDetailsAsync(location);

            if (locationDetails.IsFailed)
            {
                return locationDetails.ToResult<WeatherInfo>();
            }

            var client = _httpClientFactory.CreateClient();
            var uri = CreateUri(new Uri("https://api.open-meteo.com"), "v1/forecast", new Dictionary<string, string>{
                {"latitude", locationDetails.Value.Latitude.ToString()},
                {"longitude",locationDetails.Value.Longitude.ToString()},
                {"timezone",locationDetails.Value.TimeZone ?? "GMT"},
                {"daily","weather_code,apparent_temperature_min,apparent_temperature_max"},
                {"forecast_days","1"},
                {"hourly","apparent_temperature,weather_code"}
            });
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();

            return Convert(json, location);
        }
        catch (Exception ex)
        {
            return Result.Fail<WeatherInfo>(ex.Message);
        }
    }

    private Result<WeatherInfo> Convert(string? json, string location)
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

        var hoursToTrack = new int[] { 8, 12, 16, 20 };
        var weatherConditions = weatherInformationDto.HourlyInformation.Time
            .Zip(weatherInformationDto.HourlyInformation.ApparentTemperature, weatherInformationDto.HourlyInformation.WeatherCode)
            .Where(item => hoursToTrack.Contains(item.First.Hour))
            .Select(item => new WeatherCondition(item.First, item.Third, item.Second, "TODO: some description"))
            .ToArray();

        var dailyConditions = new DailyWeatherCondition(
                weatherInformationDto.DailyInformation.WeatherCode.First(),
                weatherInformationDto.DailyInformation.ApparentTemperatureMin.First(),
                weatherInformationDto.DailyInformation.ApparentTemperatureMax.First(),
                "TODO: some description");
        var weatherInformation = new WeatherInfo(location, dailyConditions, weatherConditions);
        return Result.Ok(weatherInformation);
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
            return Result.Fail<LocationDto>(ex.Message);
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

    internal class LocationDto
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("latitude")]
        public float Latitude { get; set; }

        [JsonProperty("longitude")]
        public float Longitude { get; set; }

        [JsonProperty("timezone")]
        public string? TimeZone { get; set; }
    }

    internal class LocationResultsDto
    {
        [JsonProperty("results")]
        public List<LocationDto> Results { get; set; } = new();
    }

    internal class DailyInformationDto
    {
        [JsonProperty("time")]
        public List<DateTime> Time { get; set; } = new();

        [JsonProperty("weather_code")]
        public List<int> WeatherCode { get; set; } = new();

        [JsonProperty("apparent_temperature_min")]
        public List<float> ApparentTemperatureMin { get; set; } = new();

        [JsonProperty("apparent_temperature_max")]
        public List<float> ApparentTemperatureMax { get; set; } = new();
    }

    internal class HourlyInformationDto
    {
        [JsonProperty("time")]
        public List<DateTime> Time { get; set; } = new();

        [JsonProperty("apparent_temperature")]
        public List<float> ApparentTemperature { get; set; } = new();

        [JsonProperty("weather_code")]
        public List<int> WeatherCode { get; set; } = new();
    }

    internal class WeatherInformationDto
    {
        [JsonProperty("hourly")]
        public HourlyInformationDto? HourlyInformation { get; set; }

        [JsonProperty("daily")]
        public DailyInformationDto? DailyInformation { get; set; }
    }
}

public record WeatherInfo(
    string Location,
    DailyWeatherCondition Daily,
    WeatherCondition[] Hourly);

public record WeatherCondition(
    DateTime Time,
    int WeatherCode,
    float Temperature,
    string Description);

public record DailyWeatherCondition(
    int WeatherCode,
    float TemperatureMin,
    float TemperatureMax,
    string Description);