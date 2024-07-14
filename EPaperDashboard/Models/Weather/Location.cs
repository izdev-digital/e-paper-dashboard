namespace EPaperDashboard.Models.Weather;

public record Location(
    string Name, 
    float Latitude, 
    float Longitude, 
    string TimeZone);