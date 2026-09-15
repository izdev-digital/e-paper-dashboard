import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type PlaywrightComponentState =
  | 'NotInstalled'
  | 'Installing'
  | 'Installed'
  | 'Incompatible'
  | 'Failed';

export interface PlaywrightComponentStatus {
  state: PlaywrightComponentState;
  appVersion: string;
  runtimeIdentifier: string;
  supported: boolean;
  installedVersion?: string;
  bytesDownloaded: number;
  totalBytes?: number;
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class PlaywrightComponentService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/system/components/rendering';

  getStatus(): Observable<PlaywrightComponentStatus> {
    return this.http.get<PlaywrightComponentStatus>(this.endpoint);
  }

  install(): Observable<PlaywrightComponentStatus> {
    return this.http.post<PlaywrightComponentStatus>(`${this.endpoint}/install`, {});
  }

  uninstall(): Observable<PlaywrightComponentStatus> {
    return this.http.delete<PlaywrightComponentStatus>(this.endpoint);
  }

  test(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.endpoint}/test`, {});
  }
}
