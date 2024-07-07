/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { BaseService } from '../base-service';
import { ApiConfiguration } from '../api-configuration';
import { StrictHttpResponse } from '../strict-http-response';

import { DateDto } from '../models/date-dto';
import { dateGet } from '../fn/date/date-get';
import { DateGet$Params } from '../fn/date/date-get';
import { dateGet$Plain } from '../fn/date/date-get-plain';
import { DateGet$Plain$Params } from '../fn/date/date-get-plain';

@Injectable({ providedIn: 'root' })
export class DateService extends BaseService {
  constructor(config: ApiConfiguration, http: HttpClient) {
    super(config, http);
  }

  /** Path part for operation `dateGet()` */
  static readonly DateGetPath = '/Date';

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `dateGet$Plain()` instead.
   *
   * This method doesn't expect any request body.
   */
  dateGet$Plain$Response(params?: DateGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<DateDto>> {
    return dateGet$Plain(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `dateGet$Plain$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  dateGet$Plain(params?: DateGet$Plain$Params, context?: HttpContext): Observable<DateDto> {
    return this.dateGet$Plain$Response(params, context).pipe(
      map((r: StrictHttpResponse<DateDto>): DateDto => r.body)
    );
  }

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `dateGet()` instead.
   *
   * This method doesn't expect any request body.
   */
  dateGet$Response(params?: DateGet$Params, context?: HttpContext): Observable<StrictHttpResponse<DateDto>> {
    return dateGet(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `dateGet$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  dateGet(params?: DateGet$Params, context?: HttpContext): Observable<DateDto> {
    return this.dateGet$Response(params, context).pipe(
      map((r: StrictHttpResponse<DateDto>): DateDto => r.body)
    );
  }

}
