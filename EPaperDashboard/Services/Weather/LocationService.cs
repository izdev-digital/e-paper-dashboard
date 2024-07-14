using EPaperDashboard.Models.Weather;
using EPaperDashboard.Services.Weather.Dto;
using EPaperDashboard.Utilities;
using FluentResults;
using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather;

public sealed class LocationService : ILocationService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LocationService(IHttpClientFactory httpClientFactory) =>
     _httpClientFactory = httpClientFactory;

    public async Task<Result<Location>> GetLocationDetailsAsync(string location)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientConfigurations.WeatherService);
            var uri = UriUtilities.CreateUri(
                new Uri("https://geocoding-api.open-meteo.com"),
                "v1/search",
                new Dictionary<string, string>{
                    {"name", location},
                    {"count", "1"},
                    {"format", "json"}});
            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            var locationResults = JsonConvert.DeserializeObject<LocationResultsDto>(json);

            return Convert(locationResults);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error("Failed to fetch location information").CausedBy(ex));
        }
    }

    private Result<Location> Convert(LocationResultsDto? locationResults) => Result
        .FailIf(locationResults is null, "Failed to deserialize location object")
        .Bind(() => Result.FailIf(!locationResults!.Results.Any(), "Information about location was not found"))
        .Bind(() =>
        {
            var location = locationResults!.Results.First();
            return Result.Merge(
                Result.FailIf(string.IsNullOrWhiteSpace(location.Name), "Location name is not available"),
                Result.FailIf(string.IsNullOrWhiteSpace(location.TimeZone), "Time zone is not available"),
                Result.FailIf(location.Latitude is null, "Latitude is not available"),
                Result.FailIf(location.Longitude is null, "Longitude is not available")
            );
        })
        .Bind<Location>(() =>
        {
            var location = locationResults!.Results.First();
            return new Location(location.Name!, location.Latitude!.Value, location.Longitude!.Value, location.TimeZone!);
        });
}
