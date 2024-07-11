/* tslint:disable */
/* eslint-disable */
import { TemperatureDto } from '../models/temperature-dto';
export interface DailyWeatherConditionDto {
  temperatureMax?: TemperatureDto;
  temperatureMin?: TemperatureDto;
  weatherCode?: number;
}
