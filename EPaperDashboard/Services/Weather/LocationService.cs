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
            var client = _httpClientFactory.CreateClient();
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

    private Result<Location> Convert(LocationResultsDto? locationResults)
    {
        if (locationResults is null)
        {
            return Result.Fail("Failed to deserialize location object");
        }

        if (!locationResults.Results.Any())
        {
            return Result.Fail("Information about location was not found");
        }

        var location = locationResults.Results.First();

        if (string.IsNullOrWhiteSpace(location.Name))
        {
            return Result.Fail("Location name is not available");
        }

        if (string.IsNullOrWhiteSpace(location.TimeZone))
        {
            return Result.Fail("Time zone is not available");
        }

        if (location.Latitude is null)
        {
            return Result.Fail("Latitude is not available");
        }

        if (location.Longitude is null)
        {
            return Result.Fail("Longitude is not available");
        }

        return new Location(location.Name, location.Latitude.Value, location.Longitude.Value, location.TimeZone);
    }
}
