import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { AuthService } from '../../services/auth.service';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-dashboard-list',
  imports: [CommonModule, RouterModule],
  template: `
    <h1>Dashboards</h1>

    <p>
      <a routerLink="/dashboards/create" class="btn btn-primary mb-3">Create New Dashboard</a>
    </p>

    @if (isLoading()) {
      <div class="text-center my-5">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (dashboards().length > 0) {
      <div class="row g-4 mb-4">
        @for (dashboard of dashboards(); track dashboard.id) {
          <div class="dashboard-tile-col">
            <div class="card h-100 shadow-sm dashboard-tile">
              <div class="card-body d-flex flex-row justify-content-between align-items-center flex-wrap">
                <div class="flex-grow-1 min-width-0">
                  <h5 class="card-title mb-2 text-truncate">{{ dashboard.name }}</h5>
                  <p class="card-text text-muted mb-2 text-truncate">{{ dashboard.description }}</p>
                  <div class="mb-2 d-flex align-items-center flex-wrap gap-2">
                    <span class="badge bg-secondary">API Key</span>
                    <span class="api-key-display" style="font-family: monospace; font-size: 0.95em; padding: 2px 6px; border-radius: 4px; word-break: break-all; max-width: 100%; display: inline-block;">{{ dashboard.apiKey }}</span>
                    <button class="btn btn-outline-secondary btn-sm" title="Copy API Key" (click)="copyApiKey(dashboard.apiKey)">
                      <i class="fa-regular fa-clipboard"></i>
                    </button>
                  </div>
                </div>
                <div class="d-flex flex-row gap-2 ms-3 mt-2 mt-md-0">
                  <a [routerLink]="['/dashboards', dashboard.id, 'edit']" class="btn btn-warning btn-sm">Edit</a>
                  <a [routerLink]="['/dashboards', dashboard.id, 'delete']" class="btn btn-danger btn-sm">Delete</a>
                </div>
              </div>
            </div>
          </div>
        }
      </div>
    } @else {
      <div class="alert alert-info">No dashboards found.</div>
    }

    @if (errorMessage()) {
      <div class="alert alert-danger">{{ errorMessage() }}</div>
    }

    @if (showCopyToast()) {
      <div class="toast align-items-center text-bg-success border-0 position-fixed bottom-0 end-0 m-4 show" role="alert">
        <div class="d-flex">
          <div class="toast-body">
            API Key copied to clipboard!
          </div>
          <button type="button" class="btn-close btn-close-white me-2 m-auto" (click)="showCopyToast.set(false)"></button>
        </div>
      </div>
    }
  `,
  styles: [`
    .dashboard-tile-col {
      width: 100%;
    }
    @media (min-width: 768px) {
      .dashboard-tile-col {
        width: 50%;
      }
    }
    @media (min-width: 1200px) {
      .dashboard-tile-col {
        width: 33.333%;
      }
    }
  `]
})
export class DashboardListComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly authService = inject(AuthService);

  // Signal-based state
  readonly dashboards = signal<Dashboard[]>([]);
  readonly isLoading = signal(false);
  readonly showCopyToast = signal(false);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    // With signals, we can synchronously check auth state
    if (this.authService.isAuthReady()) {
      this.loadDashboards();
    } else {
      // Wait for auth to be ready
      const checkInterval = setInterval(() => {
        if (this.authService.isAuthReady()) {
          clearInterval(checkInterval);
          this.loadDashboards();
        }
      }, 10);
    }
  }

  loadDashboards(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.dashboardService.getDashboards().subscribe({
      next: (dashboards) => {
        this.dashboards.set(dashboards);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading dashboards:', err);
        this.errorMessage.set('Failed to load dashboards. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  copyApiKey(apiKey: string): void {
    navigator.clipboard.writeText(apiKey).then(() => {
      this.showCopyToast.set(true);
      setTimeout(() => {
        this.showCopyToast.set(false);
      }, 3000);
    }).catch((err) => {
      console.error('Failed to copy API key:', err);
      // Fallback: show alert if clipboard fails
      alert(`API Key: ${apiKey}`);
    });
  }
}
