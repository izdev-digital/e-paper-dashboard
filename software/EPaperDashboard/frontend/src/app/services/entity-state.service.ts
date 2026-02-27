import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { HassEntityState } from '../models/types';

@Injectable({
  providedIn: 'root'
})
export class EntityStateService {
  private readonly http = inject(HttpClient);

  getEntityStates(dashboardId: string, entityIds: string[]): Observable<HassEntityState[]> {
    return this.http.post<{ data: HassEntityState[] }>(`/api/dashboards/${dashboardId}/entity-states`, {
      entityIds
    }).pipe(map(res => res.data || []));
  }
}
