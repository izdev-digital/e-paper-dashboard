import { Pipe, PipeTransform } from '@angular/core';
import { WMO_CODES } from './wmo.codes';

@Pipe({
  name: 'weatherCodeToDescription'
})
export class WeatherCodeToDescriptionPipe implements PipeTransform {
  transform(value?: number): string | undefined {
    return value ? WMO_CODES.get(value)?.day?.description : undefined;
  }
}
