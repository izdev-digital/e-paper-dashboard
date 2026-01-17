import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface DashboardOption {
  url_path: string;
  title: string;
}

@Component({
  selector: 'app-dashboard-selector-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isOpen()) {
      <div class="modal-backdrop fade show" (click)="cancel()"></div>
      <div class="modal fade show d-block" tabindex="-1" role="dialog">
        <div class="modal-dialog modal-dialog-centered modal-dialog-scrollable" role="document">
          <div class="modal-content">
            <div class="modal-header">
              <h5 class="modal-title">Select Dashboard</h5>
              <button type="button" class="btn-close" (click)="cancel()"></button>
            </div>
            <div class="modal-body">
              @if (isLoading()) {
                <div class="text-center py-4">
                  <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading dashboards...</span>
                  </div>
                  <p class="mt-2 text-muted">Fetching available dashboards...</p>
                </div>
              } @else if (error()) {
                <div class="alert alert-danger mb-0">
                  <i class="fa-solid fa-exclamation-circle me-2"></i>{{ error() }}
                </div>
              } @else if (dashboards().length === 0) {
                <div class="alert alert-info mb-0">
                  <i class="fa-solid fa-info-circle me-2"></i>No dashboards found
                </div>
              } @else {
                <div class="list-group">
                  @for (dashboard of dashboards(); track dashboard.url_path) {
                    <button 
                      type="button" 
                      class="list-group-item list-group-item-action"
                      (click)="selectDashboard(dashboard)"
                    >
                      <div class="d-flex justify-content-between align-items-center">
                        <div>
                          <h6 class="mb-1">{{ dashboard.title }}</h6>
                          <small class="d-block" style="color: var(--bs-secondary-color); font-size: 0.8rem;">
                            <code style="background-color: var(--bs-secondary-bg); padding: 0.25rem 0.5rem; border-radius: 0.25rem;">{{ dashboard.url_path }}</code>
                          </small>
                        </div>
                        <i class="fa-solid fa-chevron-right text-muted"></i>
                      </div>
                    </button>
                  }
                </div>
              }
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" (click)="cancel()">Cancel</button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-backdrop {
      z-index: 1050;
    }
    .modal {
      z-index: 1055;
    }
    .list-group-item {
      cursor: pointer;
      transition: background-color 0.15s ease-in-out;
    }
    .list-group-item:hover {
      background-color: var(--bs-secondary-bg);
    }
  `]
})
export class DashboardSelectorDialogComponent {
  readonly isOpen = signal(false);
  readonly isLoading = signal(false);
  readonly error = signal('');
  readonly dashboards = signal<DashboardOption[]>([]);
  
  private resolveCallback?: (value: string | null) => void;

  openWithLoading(): void {
    this.dashboards.set([]);
    this.error.set('');
    this.isLoading.set(true);
    this.isOpen.set(true);
  }

  open(dashboards: DashboardOption[]): Promise<string | null> {
    this.dashboards.set(dashboards);
    this.error.set('');
    this.isLoading.set(false);
    this.isOpen.set(true);
    
    return new Promise<string | null>((resolve) => {
      this.resolveCallback = resolve;
    });
  }

  selectDashboard(dashboard: DashboardOption): void {
    console.log('Dashboard selected:', dashboard);
    console.log('Dashboard url_path:', dashboard.url_path);
    console.log('Dashboard title:', dashboard.title);
    this.isOpen.set(false);
    if (this.resolveCallback) {
      console.log('Resolving with path:', dashboard.url_path);
      this.resolveCallback(dashboard.url_path);
      this.resolveCallback = undefined;
    }
  }

  setError(error: string): void {
    this.error.set(error);
    this.isLoading.set(false);
  }

  cancel(): void {
    this.isOpen.set(false);
    if (this.resolveCallback) {
      this.resolveCallback(null);
      this.resolveCallback = undefined;
    }
  }
}
