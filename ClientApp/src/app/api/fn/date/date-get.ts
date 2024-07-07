/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { StrictHttpResponse } from '../../strict-http-response';
import { RequestBuilder } from '../../request-builder';

import { DateDto } from '../../models/date-dto';

export interface DateGet$Params {
}

export function dateGet(http: HttpClient, rootUrl: string, params?: DateGet$Params, context?: HttpContext): Observable<StrictHttpResponse<DateDto>> {
  const rb = new RequestBuilder(rootUrl, dateGet.PATH, 'get');
  if (params) {
  }

  return http.request(
    rb.build({ responseType: 'json', accept: 'text/json', context })
  ).pipe(
    filter((r: any): r is HttpResponse<any> => r instanceof HttpResponse),
    map((r: HttpResponse<any>) => {
      return r as StrictHttpResponse<DateDto>;
    })
  );
}

dateGet.PATH = '/Date';
