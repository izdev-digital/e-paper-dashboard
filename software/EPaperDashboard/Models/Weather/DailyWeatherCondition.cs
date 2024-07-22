namespace EPaperDashboard.Models.Weather;

public record DailyWeatherCondition(
    int WeatherCode,
    Temperature TemperatureMin,
    Temperature TemperatureMax);