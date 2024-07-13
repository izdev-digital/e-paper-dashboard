using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal class DailyInformationUnitsDto
{
    [JsonProperty("apparent_temperature_min")]
    public string? TemperatureMinUnits { get; set; }

    [JsonProperty("apparent_temperature_max")]
    public string? TemperatureMaxUnits { get; set; }
}