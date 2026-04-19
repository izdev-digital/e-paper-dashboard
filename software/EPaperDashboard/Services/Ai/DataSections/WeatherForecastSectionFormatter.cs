using System.Text;
using System.Text.Json;

namespace EPaperDashboard.Services.Ai.DataSections;

public sealed class WeatherForecastSectionFormatter : IAiDataSectionFormatter
{
    public bool HasData(AiDataSnapshot data) => data.WeatherForecasts.Count > 0;

    public string FormatSection(AiDataSnapshot data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Weather Forecasts");
        foreach (var (entityId, forecast) in data.WeatherForecasts)
        {
            sb.AppendLine($"Forecast: {entityId} ({forecast.Count} entries)");
            foreach (var entry in forecast.Take(5))
            {
                if (entry is JsonElement je)
                {
                    var parts = new List<string>();
                    if (je.TryGetProperty("datetime", out var dt)) parts.Add(dt.GetString() ?? "");
                    if (je.TryGetProperty("condition", out var cond)) parts.Add(cond.GetString() ?? "");
                    if (je.TryGetProperty("temperature", out var temp)) parts.Add($"{temp}°");
                    if (je.TryGetProperty("templow", out var tempLow)) parts.Add($"low {tempLow}°");
                    if (je.TryGetProperty("precipitation_probability", out var precip)) parts.Add($"{precip}% precip");
                    if (je.TryGetProperty("wind_speed", out var wind)) parts.Add($"wind {wind}");
                    if (parts.Count > 0)
                        sb.AppendLine($"  - {string.Join(", ", parts)}");
                }
            }
        }
        return sb.ToString();
    }
}
