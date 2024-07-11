using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

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