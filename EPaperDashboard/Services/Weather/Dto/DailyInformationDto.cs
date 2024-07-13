using Newtonsoft.Json;

namespace EPaperDashboard.Services.Weather.Dto;

internal class DailyInformationDto
{
    [JsonProperty("time")]
    public List<DateTime> Time { get; set; } = new();

    [JsonProperty("weather_code")]
    public List<int> WeatherCode { get; set; } = new();

    [JsonProperty("apparent_temperature_min")]
    public List<float> ApparentTemperatureMin { get; set; } = new();

    [JsonProperty("apparent_temperature_max")]
    public List<float> ApparentTemperatureMax { get; set; } = new();
}