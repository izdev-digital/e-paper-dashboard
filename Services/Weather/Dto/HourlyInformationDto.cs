using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal class HourlyInformationDto
{
    [JsonProperty("time")]
    public List<DateTime> Time { get; set; } = new();

    [JsonProperty("apparent_temperature")]
    public List<float> ApparentTemperature { get; set; } = new();

    [JsonProperty("weather_code")]
    public List<int> WeatherCode { get; set; } = new();
}