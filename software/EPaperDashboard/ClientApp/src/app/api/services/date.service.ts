/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { BaseService } from '../base-service';
import { ApiConfiguration } from '../api-configuration';
import { StrictHttpResponse } from '../strict-http-response';

import { apiDateGet } from '../fn/date/api-date-get';
import { ApiDateGet$Params } from '../fn/date/api-date-get';
import { apiDateGet$Plain } from '../fn/date/api-date-get-plain';
import { ApiDateGet$Plain$Params } from '../fn/date/api-date-get-plain';
import { DateDto } from '../models/date-dto';

@Injectable({ providedIn: 'root' })
export class DateService extends BaseService {
  constructor(config: ApiConfiguration, http: HttpClient) {
    super(config, http);
  }

  /** Path part for operation `apiDateGet()` */
  static readonly ApiDateGetPath = '/api/date';

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `apiDateGet$Plain()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiDateGet$Plain$Response(params?: ApiDateGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<DateDto>> {
    return apiDateGet$Plain(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `apiDateGet$Plain$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiDateGet$Plain(params?: ApiDateGet$Plain$Params, context?: HttpContext): Observable<DateDto> {
    return this.apiDateGet$Plain$Response(params, context).pipe(
      map((r: StrictHttpResponse<DateDto>): DateDto => r.body)
    );
  }

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `apiDateGet()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiDateGet$Response(params?: ApiDateGet$Params, context?: HttpContext): Observable<StrictHttpResponse<DateDto>> {
    return apiDateGet(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `apiDateGet$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiDateGet(params?: ApiDateGet$Params, context?: HttpContext): Observable<DateDto> {
    return this.apiDateGet$Response(params, context).pipe(
      map((r: StrictHttpResponse<DateDto>): DateDto => r.body)
    );
  }

}
