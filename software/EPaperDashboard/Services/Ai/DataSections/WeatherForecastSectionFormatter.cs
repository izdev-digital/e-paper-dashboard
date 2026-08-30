using System.Text;

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
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(entry.Datetime)) parts.Add(entry.Datetime);
                if (!string.IsNullOrWhiteSpace(entry.Condition)) parts.Add(entry.Condition);
                if (entry.Temperature is not null) parts.Add($"{entry.Temperature}°");
                if (entry.TempLow is not null) parts.Add($"low {entry.TempLow}°");
                if (entry.PrecipitationProbability is not null) parts.Add($"{entry.PrecipitationProbability}% precip");
                if (entry.WindSpeed is not null) parts.Add($"wind {entry.WindSpeed}");
                if (parts.Count > 0) sb.AppendLine($"  - {string.Join(", ", parts)}");
            }
        }
        return sb.ToString();
    }
}
