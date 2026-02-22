import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface FirmwareInfo {
  version: string | null;
  releaseNotes?: string;
  publishedAt?: string;
  fileSize?: number;
  hasDownload: boolean;
  isUpdateAvailable: boolean;
  message?: string;
}

@Injectable({
  providedIn: 'root'
})
export class FirmwareService {
  private readonly http = inject(HttpClient);

  readonly firmwareInfo = signal<FirmwareInfo | null>(null);
  readonly isLoading = signal(false);
  readonly error = signal<string>('');

  loadLatestFirmware(): void {
    this.isLoading.set(true);
    this.error.set('');
    this.http.get<FirmwareInfo>('/api/firmware/latest').subscribe({
      next: (info) => {
        this.firmwareInfo.set(info);
        this.isLoading.set(false);
      },
      error: () => {
        this.error.set('Failed to load firmware info');
        this.isLoading.set(false);
      }
    });
  }

  refreshFirmwareCheck(): void {
    this.isLoading.set(true);
    this.error.set('');
    this.http.post<FirmwareInfo>('/api/firmware/refresh', {}).subscribe({
      next: (info) => {
        this.firmwareInfo.set(info);
        this.isLoading.set(false);
      },
      error: () => {
        this.error.set('Failed to check for firmware updates');
        this.isLoading.set(false);
      }
    });
  }
}
