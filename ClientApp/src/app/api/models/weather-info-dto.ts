/* tslint:disable */
/* eslint-disable */
import { DailyWeatherConditionDto } from '../models/daily-weather-condition-dto';
import { WeatherConditionDto } from '../models/weather-condition-dto';
export interface WeatherInfoDto {
  daily?: DailyWeatherConditionDto;
  hourly?: Array<WeatherConditionDto> | null;
  location?: string | null;
}
