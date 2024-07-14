using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal sealed class HourlyInformationUnitsDto
{
    [JsonProperty("apparent_temperature")]
    public string? TemperatureUnits { get; set; }
}