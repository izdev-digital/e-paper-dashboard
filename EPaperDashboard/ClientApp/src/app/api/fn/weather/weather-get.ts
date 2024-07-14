/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { WeatherInfoDto } from '../../models/weather-info-dto';

export interface WeatherGet$Params {
}

export function weatherGet(http: HttpClient, rootUrl: string, params?: WeatherGet$Params, context?: HttpContext): Observable<StrictHttpResponse<WeatherInfoDto>> {
  const rb = new RequestBuilder(rootUrl, weatherGet.PATH, 'get');
  if (params) {
  }

  return http.request(
    rb.build({ responseType: 'json', accept: 'text/json', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<WeatherInfoDto>;
    })
  );
}

weatherGet.PATH = '/Weather';
