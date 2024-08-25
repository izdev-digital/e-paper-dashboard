/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { DateDto } from '../../models/date-dto';

export interface ApiDateGet$Plain$Params {
}

export function apiDateGet$Plain(http: HttpClient, rootUrl: string, params?: ApiDateGet$Plain$Params, context?: HttpContext): Observable<StrictHttpResponse<DateDto>> {
  const rb = new RequestBuilder(rootUrl, apiDateGet$Plain.PATH, 'get');
  if (params) {
  }

  return http.request(
    rb.build({ responseType: 'text', accept: 'text/plain', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<DateDto>;
    })
  );
}

apiDateGet$Plain.PATH = '/api/date';
