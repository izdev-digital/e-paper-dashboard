/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { BaseService } from '../base-service';
import { ApiConfiguration } from '../api-configuration';
import { StrictHttpResponse } from '../strict-http-response';

import { weatherGet } from '../fn/weather/weather-get';
import { WeatherGet$Params } from '../fn/weather/weather-get';
import { weatherGet$Plain } from '../fn/weather/weather-get-plain';
import { WeatherGet$Plain$Params } from '../fn/weather/weather-get-plain';
import { WeatherInfoDto } from '../models/weather-info-dto';

@Injectable({ providedIn: 'root' })
export class WeatherService extends BaseService {
  constructor(config: ApiConfiguration, http: HttpClient) {
    super(config, http);
  }

  /** Path part for operation `weatherGet()` */
  static readonly WeatherGetPath = '/Weather';

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `weatherGet$Plain()` instead.
   *
   * This method doesn't expect any request body.
   */
  weatherGet$Plain$Response(params?: WeatherGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<WeatherInfoDto>> {
    return weatherGet$Plain(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `weatherGet$Plain$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  weatherGet$Plain(params?: WeatherGet$Plain$Params, context?: HttpContext): Observable<WeatherInfoDto> {
    return this.weatherGet$Plain$Response(params, context).pipe(
      map((r: StrictHttpResponse<WeatherInfoDto>): WeatherInfoDto => r.body)
    );
  }

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `weatherGet()` instead.
   *
   * This method doesn't expect any request body.
   */
  weatherGet$Response(params?: WeatherGet$Params, context?: HttpContext): Observable<StrictHttpResponse<WeatherInfoDto>> {
    return weatherGet(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `weatherGet$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  weatherGet(params?: WeatherGet$Params, context?: HttpContext): Observable<WeatherInfoDto> {
    return this.weatherGet$Response(params, context).pipe(
      map((r: StrictHttpResponse<WeatherInfoDto>): WeatherInfoDto => r.body)
    );
  }

}
