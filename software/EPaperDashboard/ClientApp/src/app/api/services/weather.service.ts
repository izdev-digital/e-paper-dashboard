/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { BaseService } from '../base-service';
import { ApiConfiguration } from '../api-configuration';
import { StrictHttpResponse } from '../strict-http-response';

import { apiWeatherGet } from '../fn/weather/api-weather-get';
import { ApiWeatherGet$Params } from '../fn/weather/api-weather-get';
import { apiWeatherGet$Plain } from '../fn/weather/api-weather-get-plain';
import { ApiWeatherGet$Plain$Params } from '../fn/weather/api-weather-get-plain';
import { WeatherInfoDto } from '../models/weather-info-dto';

@Injectable({ providedIn: 'root' })
export class WeatherService extends BaseService {
  constructor(config: ApiConfiguration, http: HttpClient) {
    super(config, http);
  }

  /** Path part for operation `apiWeatherGet()` */
  static readonly ApiWeatherGetPath = '/api/weather';

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `apiWeatherGet$Plain()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiWeatherGet$Plain$Response(params?: ApiWeatherGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<WeatherInfoDto>> {
    return apiWeatherGet$Plain(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `apiWeatherGet$Plain$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiWeatherGet$Plain(params?: ApiWeatherGet$Plain$Params, context?: HttpContext): Observable<WeatherInfoDto> {
    return this.apiWeatherGet$Plain$Response(params, context).pipe(
      map((r: StrictHttpResponse<WeatherInfoDto>): WeatherInfoDto => r.body)
    );
  }

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `apiWeatherGet()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiWeatherGet$Response(params?: ApiWeatherGet$Params, context?: HttpContext): Observable<StrictHttpResponse<WeatherInfoDto>> {
    return apiWeatherGet(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `apiWeatherGet$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiWeatherGet(params?: ApiWeatherGet$Params, context?: HttpContext): Observable<WeatherInfoDto> {
    return this.apiWeatherGet$Response(params, context).pipe(
      map((r: StrictHttpResponse<WeatherInfoDto>): WeatherInfoDto => r.body)
    );
  }

}
