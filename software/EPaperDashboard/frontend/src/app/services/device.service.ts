import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Device {
  id: string;
  dashboardId: string;
  deviceIdentifier: string;
  name: string;
  pairedAt: string;
  lastSeenAt?: string;
}

export interface StartPairingResponse {
  code: string;
  expiresAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class DeviceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api';

  getDevicesForDashboard(dashboardId: string): Observable<Device[]> {
    return this.http.get<Device[]>(`${this.baseUrl}/devices/dashboard/${dashboardId}`);
  }

  deleteDevice(deviceId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/devices/${deviceId}`);
  }

  startPairing(dashboardId: string): Observable<StartPairingResponse> {
    return this.http.post<StartPairingResponse>(`${this.baseUrl}/pairing/start`, { dashboardId });
  }
}
