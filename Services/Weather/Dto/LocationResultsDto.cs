using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal class LocationResultsDto
{
    [JsonProperty("results")]
    public List<LocationDto> Results { get; set; } = new();
}