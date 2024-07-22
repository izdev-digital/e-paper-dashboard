import { Pipe, PipeTransform } from '@angular/core';
import { WMO_CODES } from './wmo.codes';

@Pipe({
  name: 'weatherCodeToImage'
})
export class WeatherCodeToImagePipe implements PipeTransform {
  transform(value?: number): string | undefined {
    return value ? WMO_CODES.get(value)?.day?.image : undefined;
  }
}
