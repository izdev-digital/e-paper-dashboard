import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class HomeAssistantService {
  private readonly http = inject(HttpClient);

  startAuth(host: string, dashboardId: string): Observable<{ authUrl: string; state: string }> {
    return this.http.post<{ authUrl: string; state: string }>('/api/homeassistant/start-auth', {
      host: host,
      dashboardId: dashboardId
    });
  }

  getDashboards(host: string, dashboardId: string): Observable<any[]> {
    // Fetch dashboards from Home Assistant through our backend
    // Backend uses the stored access token from the dashboard record
    return this.http.post<{ dashboards: any[] }>('/api/homeassistant/fetch-dashboards', {
      dashboardId: dashboardId
    }).pipe(
      map(response => response.dashboards || [])
    );
  }
}



