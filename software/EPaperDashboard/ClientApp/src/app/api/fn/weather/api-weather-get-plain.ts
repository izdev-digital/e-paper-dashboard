/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { WeatherInfoDto } from '../../models/weather-info-dto';

export interface ApiWeatherGet$Plain$Params {
}

export function apiWeatherGet$Plain(http: HttpClient, rootUrl: string, params?: ApiWeatherGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<WeatherInfoDto>> {
  const rb = new RequestBuilder(rootUrl, apiWeatherGet$Plain.PATH, 'get');
  if (params) {
  }

  return http.request(
    rb.build({ responseType: 'text', accept: 'text/plain', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<WeatherInfoDto>;
    })
  );
}

apiWeatherGet$Plain.PATH = '/api/weather';
