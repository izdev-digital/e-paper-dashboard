import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Dashboard, CreateDashboardRequest, UpdateDashboardRequest } from '../models/types';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly http = inject(HttpClient);

  getDashboards(): Observable<Dashboard[]> {
    return this.http.get<Dashboard[]>('/api/dashboards');
  }

  getDashboard(id: string): Observable<Dashboard> {
    return this.http.get<Dashboard>(`/api/dashboards/${id}`);
  }

  createDashboard(request: CreateDashboardRequest): Observable<Dashboard> {
    return this.http.post<Dashboard>('/api/dashboards', request);
  }

  updateDashboard(id: string, request: UpdateDashboardRequest): Observable<Dashboard> {
    return this.http.put<Dashboard>(`/api/dashboards/${id}`, request);
  }

  deleteDashboard(id: string): Observable<any> {
    return this.http.delete(`/api/dashboards/${id}`);
  }
}
