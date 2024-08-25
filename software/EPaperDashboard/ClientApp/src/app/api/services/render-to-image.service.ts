/* tslint:disable */
/* eslint-disable */
import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import { BaseService } from '../base-service';
import { ApiConfiguration } from '../api-configuration';
import { StrictHttpResponse } from '../strict-http-response';

import { apiRenderImageGet } from '../fn/render-to-image/api-render-image-get';
import { ApiRenderImageGet$Params } from '../fn/render-to-image/api-render-image-get';
import { apiRenderTextGet } from '../fn/render-to-image/api-render-text-get';
import { ApiRenderTextGet$Params } from '../fn/render-to-image/api-render-text-get';

@Injectable({ providedIn: 'root' })
export class RenderToImageService extends BaseService {
  constructor(config: ApiConfiguration, http: HttpClient) {
    super(config, http);
  }

  /** Path part for operation `apiRenderTextGet()` */
  static readonly ApiRenderTextGetPath = '/api/render/text';

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `apiRenderTextGet()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiRenderTextGet$Response(params?: ApiRenderTextGet$Params, context?: HttpContext): Observable<StrictHttpResponse<void>> {
    return apiRenderTextGet(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `apiRenderTextGet$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiRenderTextGet(params?: ApiRenderTextGet$Params, context?: HttpContext): Observable<void> {
    return this.apiRenderTextGet$Response(params, context).pipe(
      map((r: StrictHttpResponse<void>): void => r.body)
    );
  }

  /** Path part for operation `apiRenderImageGet()` */
  static readonly ApiRenderImageGetPath = '/api/render/image';

  /**
   * This method provides access to the full `HttpResponse`, allowing access to response headers.
   * To access only the response body, use `apiRenderImageGet()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiRenderImageGet$Response(params?: ApiRenderImageGet$Params, context?: HttpContext): Observable<StrictHttpResponse<void>> {
    return apiRenderImageGet(this.http, this.rootUrl, params, context);
  }

  /**
   * This method provides access only to the response body.
   * To access the full response (for headers, for example), `apiRenderImageGet$Response()` instead.
   *
   * This method doesn't expect any request body.
   */
  apiRenderImageGet(params?: ApiRenderImageGet$Params, context?: HttpContext): Observable<void> {
    return this.apiRenderImageGet$Response(params, context).pipe(
      map((r: StrictHttpResponse<void>): void => r.body)
    );
  }

}
