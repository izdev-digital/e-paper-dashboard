import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class WeatherService {
  private readonly http = inject(HttpClient);

  getWeatherForecast(dashboardId: string, weatherEntityId: string, forecastType: string = 'daily'): Observable<any> {
    return this.http.get<{ data: any }>(`/api/dashboards/${dashboardId}/weather-forecast/${weatherEntityId}?forecastType=${forecastType}`).pipe(
      map(response => response.data)
    );
  }
}
