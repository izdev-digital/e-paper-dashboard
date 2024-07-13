/* tslint:disable */
/* eslint-disable */
import { TemperatureDto } from '../models/temperature-dto';
export interface WeatherConditionDto {
  temperature?: TemperatureDto;
  time?: string;
  weatherCode?: number;
}
