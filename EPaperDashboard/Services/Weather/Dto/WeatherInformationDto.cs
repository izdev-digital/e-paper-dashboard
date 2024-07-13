using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal class WeatherInformationDto
{
    [JsonProperty("hourly")]
    public HourlyInformationDto? HourlyInformation { get; set; }

    [JsonProperty("hourly_units")]
    public HourlyInformationUnitsDto? HourlyInformationUnits { get; set; }

    [JsonProperty("daily")]
    public DailyInformationDto? DailyInformation { get; set; }

    [JsonProperty("daily_units")]
    public DailyInformationUnitsDto? DailyInformationUnits { get; set; }
}