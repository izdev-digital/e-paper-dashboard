import { WeatherInfoDto } from "../api/models/weather-info-dto";

export interface DailyWeatherCondition {
    temperatureMax?: number;
    temperatureMin?: number;
    weatherCode?: number;
}

export interface HourlyWeatherCondition {
    temperature?: number;
    time?: Date;
    weatherCode?: number;
}

export interface WeatherInfo {
    daily?: DailyWeatherCondition;
    hourly?: Array<HourlyWeatherCondition> | null;
    location?: string | null;
}

export function convertToDomain(weatherInfo: WeatherInfoDto): WeatherInfo {
    const hoursToDisplay = [8, 12, 16, 20];
    return {
      location: weatherInfo.location,
      daily: {
        weatherCode: weatherInfo.daily?.weatherCode,
        temperatureMin: weatherInfo.daily?.temperatureMin,
        temperatureMax: weatherInfo.daily?.temperatureMax
      },
      hourly: weatherInfo.hourly?.map((x) => {
        return {
          time: new Date(x.time ?? ""),
          temperature: x.temperature,
          weatherCode: x.weatherCode
        };
      }).filter((x) => hoursToDisplay.findIndex((hour) => x.time.getHours() === hour) >= 0)
    }
  }