import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class CalendarService {
  private readonly http = inject(HttpClient);

  getCalendarEvents(dashboardId: string, calendarEntityId: string, hoursAhead: number = 168): Observable<any[]> {
    return this.http.get<{ data: any[] }>(`/api/dashboards/${dashboardId}/calendar-events/${calendarEntityId}?hoursAhead=${hoursAhead}`).pipe(
      map(response => response.data || [])
    );
  }
}
