using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal class HourlyInformationUnitsDto
{
    [JsonProperty("apparent_temperature")]
    public string? TemperatureUnits { get; set; }
}