namespace EPaperDashboard.Models.Weather;

public record WeatherInfo(
    string Location,
    DailyWeatherCondition Daily,
    WeatherCondition[] Hourly);
