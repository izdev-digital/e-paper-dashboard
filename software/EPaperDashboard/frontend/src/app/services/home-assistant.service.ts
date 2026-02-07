
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { HassEntityState } from '../models/types';

export interface HassEntity {
  entityId: string;
  friendlyName: string;
  domain: string;
  deviceClass?: string | null;
  unitOfMeasurement?: string | null;
  icon?: string | null;
  state?: string | null;
  supportedFeatures?: number | null;
}

export interface TodoItem {
  summary: string;
  status: string;
  uid: string;
}

export interface HistoryState {
  state: string;
  numericValue: number;
  lastChanged: string;
  attributes: Record<string, any>;
}

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

  getDashboards(dashboardId: string): Observable<any[]> {
    return this.http.get<{ data: any[] }>(`/api/dashboards/${dashboardId}/homeassistant/dashboards`).pipe(
      map(response => response.data || [])
    );
  }

  getEntities(dashboardId: string): Observable<HassEntity[]> {
    return this.http.get<{ data: HassEntity[] }>(`/api/dashboards/${dashboardId}/homeassistant/designer/entity-metadata`).pipe(
      map(response => response.data || [])
    );
  }

  getEntityStates(dashboardId: string, entityIds: string[]): Observable<HassEntityState[]> {
    return this.http.post<{ data: HassEntityState[] }>(`/api/dashboards/${dashboardId}/homeassistant/entity-states`, {
      entityIds
    }).pipe(map(res => res.data || []));
  }

  getEntityHistory(dashboardId: string, entityIds: string[], hours: number = 24): Observable<Record<string, HistoryState[]>> {
    return this.http.post<{ data: Record<string, HistoryState[]> }>(`/api/dashboards/${dashboardId}/homeassistant/entity-history`, {
      entityIds,
      hours
    }).pipe(
      map(response => response.data || {})
    );
  }

  getTodoItems(dashboardId: string, todoEntityId: string): Observable<TodoItem[]> {
    return this.http.get<{ data: TodoItem[] }>(`/api/dashboards/${dashboardId}/homeassistant/todo-items/${todoEntityId}`).pipe(
      map(response => response.data || [])
    );
  }

  getCalendarEvents(dashboardId: string, calendarEntityId: string, hoursAhead: number = 168): Observable<any[]> {
    return this.http.get<{ data: any[] }>(`/api/dashboards/${dashboardId}/homeassistant/calendar-events/${calendarEntityId}?hoursAhead=${hoursAhead}`).pipe(
      map(response => response.data || [])
    );
  }

  getWeatherForecast(dashboardId: string, weatherEntityId: string, forecastType: string = 'daily'): Observable<any> {
    return this.http.get<{ data: any }>(`/api/dashboards/${dashboardId}/homeassistant/weather-forecast/${weatherEntityId}?forecastType=${forecastType}`).pipe(
      map(response => response.data)
    );
  }
}