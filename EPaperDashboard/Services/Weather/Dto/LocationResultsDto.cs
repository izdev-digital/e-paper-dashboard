using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal sealed class LocationResultsDto
{
    [JsonProperty("results")]
    public List<LocationDto> Results { get; set; } = new();
}