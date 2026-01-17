import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class VersionService {
  private readonly http = inject(HttpClient);
  
  readonly version = signal<string>('');

  constructor() {
    this.loadVersion();
  }

  private loadVersion(): void {
    // Try to get version from a version endpoint
    // For now, we'll try to extract it from the app info or use a default
    this.http.get<{ version: string }>('/api/app/version').subscribe({
      next: (response) => {
        this.version.set(response.version);
      },
      error: () => {
        // Fallback version if endpoint doesn't exist yet
        this.version.set('0.0.6');
      }
    });
  }
}
