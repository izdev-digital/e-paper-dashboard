import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface HistoryState {
  state: string;
  numericValue: number;
  lastChanged: string;
  attributes: Record<string, any>;
}

@Injectable({
  providedIn: 'root'
})
export class EntityHistoryService {
  private readonly http = inject(HttpClient);

  getEntityHistory(dashboardId: string, entityIds: string[], hours: number = 24): Observable<Record<string, HistoryState[]>> {
    return this.http.post<{ data: Record<string, HistoryState[]> }>(`/api/dashboards/${dashboardId}/entity-history`, {
      entityIds,
      hours
    }).pipe(
      map(response => response.data || {})
    );
  }
}
