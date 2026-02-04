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
    this.http.get<{ version: string }>('/api/app/version').subscribe({
      next: (response) => {
        this.version.set(response.version);
      },
      error: () => {
        this.version.set('0.0.6');
      }
    });
  }
}
