import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { AuthService } from '../../services/auth.service';
import { DialogService } from '../../services/dialog.service';
import { ToastService } from '../../services/toast.service';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-dashboard-list',
  standalone: true,
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
      <div class="dashboard-list">
        @for (dashboard of dashboards(); track dashboard.id) {
          <div class="dashboard-card">
            <div class="dashboard-card-header">
              <div class="dashboard-info">
                <h5 class="dashboard-title">{{ dashboard.name }}</h5>
              </div>
              <div class="dashboard-actions">
                <button type="button" class="btn btn-outline-primary btn-sm" (click)="editDashboard(dashboard.id)">
                  <i class="fa-solid fa-pen-to-square me-1"></i> Edit
                </button>
                <button type="button" class="btn btn-outline-danger btn-sm" (click)="deleteDashboard(dashboard.id)">
                  <i class="fa-solid fa-trash me-1"></i> Delete
                </button>
              </div>
            </div>
            <div class="dashboard-card-body">
              <div class="api-key-section">
                <span class="api-key-label">API Key</span>
                <div class="api-key-container">
                  <code class="api-key-value">{{ dashboard.apiKey }}</code>
                  <button class="btn btn-sm btn-outline-secondary" title="Copy API Key" (click)="copyApiKey(dashboard.apiKey)">
                    <i class="fa-regular fa-clipboard"></i>
                  </button>
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
  `,
  styles: [`
    .dashboard-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      margin-bottom: 2rem;
    }

    .dashboard-card {
      background: var(--bs-body-bg);
      border: 1px solid var(--bs-border-color);
      border-radius: 0.5rem;
      overflow: hidden;
      transition: all 0.2s ease;
    }

    .dashboard-card:hover {
      box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15);
      transform: translateY(-2px);
    }

    .dashboard-card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.25rem 1.5rem;
      background: var(--bs-secondary-bg);
      border-bottom: 1px solid var(--bs-border-color);
      gap: 1rem;
      flex-wrap: wrap;
    }

    .dashboard-info {
      flex: 1;
      min-width: 0;
    }

    .dashboard-title {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 600;
      color: var(--bs-body-color);
    }

    .dashboard-description {
      margin: 0.25rem 0 0 0;
      color: var(--bs-secondary-color);
      font-size: 0.9rem;
    }

    .dashboard-actions {
      display: flex;
      gap: 0.5rem;
      flex-shrink: 0;
    }

    .dashboard-card-body {
      padding: 1.25rem 1.5rem;
    }

    .api-key-section {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .api-key-label {
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--bs-secondary-color);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .api-key-container {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem;
      background: var(--bs-secondary-bg);
      border: 1px solid var(--bs-border-color);
      border-radius: 0.375rem;
    }

    .api-key-value {
      flex: 1;
      font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
      font-size: 0.875rem;
      color: var(--bs-body-color);
      word-break: break-all;
      background: transparent;
      border: none;
      padding: 0;
    }

    @media (max-width: 768px) {
      .dashboard-card-header {
        flex-direction: column;
        align-items: flex-start;
      }

      .dashboard-actions {
        width: 100%;
        justify-content: stretch;
      }

      .dashboard-actions button {
        flex: 1;
      }
    }
  `]
})
export class DashboardListComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialogService = inject(DialogService);
  private readonly toastService = inject(ToastService);

  // Signal-based state
  readonly dashboards = signal<Dashboard[]>([]);
  readonly isLoading = signal(false);
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
      this.toastService.success('API Key copied to clipboard!');
    }).catch((err) => {
      console.error('Failed to copy API key:', err);
      this.toastService.error('Failed to copy API key');
    });
  }

  editDashboard(id: string): void {
    this.router.navigate(['/dashboards', id, 'edit']);
  }

  async deleteDashboard(id: string): Promise<void> {
    const dashboard = this.dashboards().find(d => d.id === id);
    if (!dashboard) return;

    await this.dialogService.confirm({
      title: 'Delete Dashboard?',
      message: `Are you sure you want to delete "${dashboard.name}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      isDangerous: true,
      onConfirm: async () => {
        try {
          await this.dashboardService.deleteDashboard(id).toPromise();
          this.toastService.success('Dashboard deleted successfully');
          this.loadDashboards();
        } catch (error: any) {
          this.toastService.error(error.error?.message || 'Failed to delete dashboard');
        }
      }
    });
  }
}
