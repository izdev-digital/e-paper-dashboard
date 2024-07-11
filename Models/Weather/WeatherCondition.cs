namespace EPaperDashboard.Models.Weather;

public record WeatherCondition(
    DateTime Time,
    int WeatherCode,
    Temperature Temperature);
