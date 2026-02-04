
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { HassEntityState } from '../models/types';

export interface HassEntity {
  entityId: string;
  friendlyName: string;
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

  getDashboards(host: string, dashboardId: string): Observable<any[]> {
    return this.http.post<{ dashboards: any[] }>('/api/homeassistant/fetch-dashboards', {
      dashboardId: dashboardId
    }).pipe(
      map(response => response.dashboards || [])
    );
  }

  getEntities(dashboardId: string): Observable<HassEntity[]> {
    return this.http.post<{ entities: HassEntity[] }>('/api/homeassistant/fetch-entities', {
      dashboardId: dashboardId
    }).pipe(
      map(response => response.entities || [])
    );
  }

  getEntityStates(dashboardId: string, entityIds: string[]): Observable<HassEntityState[]> {
    return this.http.post<{ states: HassEntityState[] }>('/api/homeassistant/fetch-entity-states', {
      dashboardId,
      entityIds
    }).pipe(map(res => res.states || []));
  }

  getEntityHistory(dashboardId: string, entityIds: string[], hours: number = 24): Observable<Record<string, HistoryState[]>> {
    return this.http.post<{ history: Record<string, HistoryState[]> }>('/api/homeassistant/fetch-entity-history', {
      dashboardId,
      entityIds,
      hours
    }).pipe(
      map(response => response.history || {})
    );  }

  getTodoItems(dashboardId: string, todoEntityId: string): Observable<TodoItem[]> {
    return this.http.get<TodoItem[]>(`/api/homeassistant/${dashboardId}/todo-items/${todoEntityId}`);
  }

  getCalendarEvents(dashboardId: string, calendarEntityId: string, hoursAhead: number = 168): Observable<any[]> {
    return this.http.get<{ events: any[] }>(`/api/homeassistant/${dashboardId}/calendar-events/${calendarEntityId}?hoursAhead=${hoursAhead}`).pipe(
      map(response => response.events || [])
    );
  }
}