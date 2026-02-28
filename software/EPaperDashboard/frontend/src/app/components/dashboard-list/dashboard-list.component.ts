import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DashboardService } from '../../services/dashboard.service';
import { AuthService } from '../../services/auth.service';
import { DialogService } from '../../services/dialog.service';
import { ToastService } from '../../services/toast.service';
import { ToastContainerComponent } from '../toast-container/toast-container.component';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-dashboard-list',
  standalone: true,
  imports: [CommonModule, RouterModule, ToastContainerComponent],
  template: `
    <app-toast-container></app-toast-container>
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h1 class="mb-0">Dashboards</h1>
      <a routerLink="/dashboards/create" class="btn btn-primary">
        <i class="fa-solid fa-plus"></i> Create New Dashboard
      </a>
    </div>

    @if (isLoading()) {
      <div class="text-center my-5">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (dashboards().length > 0) {
      <div class="dashboard-list">
        @for (dashboard of dashboards(); track dashboard.id) {
          <div class="dashboard-item">
            <h5 class="dashboard-title">{{ dashboard.name }}</h5>
            <div class="dashboard-actions">
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="editDashboard(dashboard.id)">
                <i class="fa-solid fa-pen-to-square"></i>
              </button>
              <button type="button" class="btn btn-sm btn-outline-danger" (click)="deleteDashboard(dashboard.id)">
                <i class="fa-solid fa-trash"></i>
              </button>
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
      gap: 0.5rem;
      margin-bottom: 2rem;
    }

    .dashboard-item {
      display: grid;
      grid-template-columns: 1fr auto;
      align-items: center;
      gap: 1rem;
      padding: 0.75rem 1rem;
      background: var(--bs-body-bg);
      border: 1px solid var(--bs-border-color);
      border-radius: 0.375rem;
      transition: all 0.15s ease;
    }

    .dashboard-item:hover {
      background: var(--bs-secondary-bg);
      border-color: var(--bs-primary);
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
    }

    .dashboard-title {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 600;
      color: var(--bs-body-color);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      grid-column: 1;
    }

    .dashboard-actions {
      display: flex;
      gap: 0.375rem;
      justify-self: end;
    }

    .dashboard-actions .btn {
      padding: 0.375rem 0.625rem;
      font-size: 0.8rem;
      min-width: 32px;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    @media (max-width: 1200px) {
      .dashboard-item {
        grid-template-columns: 1fr auto;
      }
    }

    @media (max-width: 1024px) {
      .dashboard-item {
        grid-template-columns: 1fr auto;
      }

      .dashboard-title {
        font-size: 1rem;
      }
    }

    @media (max-width: 768px) {
      .dashboard-item {
        grid-template-columns: 1fr;
        gap: 0.5rem;
      }

      .dashboard-title {
        grid-column: 1;
        white-space: normal;
      }

      .dashboard-actions {
        grid-column: 1;
        justify-self: stretch;
        width: 100%;
      }

      .dashboard-actions .btn {
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
        this.errorMessage.set('Failed to load dashboards. Please try again.');
        this.isLoading.set(false);
      }
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
        // Optimistically remove from UI and allow undo
        this.dashboards.update(list => list.filter(d => d.id !== id));

        let didUndo = false;

        const performDelete = async () => {
          try {
            await firstValueFrom(this.dashboardService.deleteDashboard(id));
            if (!didUndo) {
              this.toastService.success('Dashboard deleted successfully');
            }
          } catch (error: any) {
            // On error, reload list and show error
            this.toastService.error(error.error?.message || 'Failed to delete dashboard');
            this.loadDashboards();
          }
        };

        const timeoutMs = 5000;
        const timeoutId = setTimeout(() => {
          performDelete();
        }, timeoutMs);

        this.toastService.showWithAction(
          `Dashboard "${dashboard.name}" deleted`,
          'Undo',
          () => {
            didUndo = true;
            clearTimeout(timeoutId);
            // restore dashboard in UI
            this.dashboards.update(list => [dashboard, ...list]);
            this.toastService.info('Deletion undone');
          },
          'info',
          timeoutMs
        );
      }
    });
  }
}
