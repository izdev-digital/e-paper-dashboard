import { WeatherInfoDto } from "../api/models/weather-info-dto";

export interface Temperature {
  value?: number;
  units?: string | null;
}

export interface DailyWeatherCondition {
  temperatureMax?: Temperature;
  temperatureMin?: Temperature;
  weatherCode?: number;
}

export interface HourlyWeatherCondition {
  temperature?: Temperature;
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
      temperatureMin: {
        value: weatherInfo.daily?.temperatureMin?.value,
        units:  weatherInfo.daily?.temperatureMin?.units
      },
      temperatureMax: {
        value: weatherInfo.daily?.temperatureMax?.value,
        units: weatherInfo.daily?.temperatureMax?.units
      }
    },
    hourly: weatherInfo.hourly?.map((x) => {
      return {
        time: new Date(x.time ?? ""),
        temperature: {
          value: x.temperature?.value,
          units: x.temperature?.units
        },
        weatherCode: x.weatherCode
      };
    }).filter((x) => hoursToDisplay.findIndex((hour) => x.time.getHours() === hour) >= 0)
  }
}