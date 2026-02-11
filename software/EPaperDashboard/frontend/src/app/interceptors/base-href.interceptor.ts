import { Injectable, inject } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DOCUMENT } from '@angular/common';

/**
 * HTTP Interceptor that prepends the base href to API requests.
 * This is necessary for Home Assistant ingress support, where the app
 * is served under a path like /api/hassio_ingress/xxx/ and API calls
 * need to go through the same path.
 */
@Injectable()
export class BaseHrefInterceptor implements HttpInterceptor {
  private readonly document = inject(DOCUMENT);

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    // Only modify requests that start with /api/
    if (!req.url.startsWith('/api/')) {
      return next.handle(req);
    }

    // Get the base href from the document
    const baseElement = this.document.querySelector('base');
    const baseHref = baseElement?.getAttribute('href') || '/';

    // If base href is just '/', no modification needed
    if (baseHref === '/') {
      return next.handle(req);
    }

    // Prepend base href to the URL (removing the leading / from the original URL)
    // baseHref already ends with /, so we need to remove the leading / from req.url
    const newUrl = baseHref + req.url.substring(1);

    const modifiedReq = req.clone({ url: newUrl });
    return next.handle(modifiedReq);
  }
}
