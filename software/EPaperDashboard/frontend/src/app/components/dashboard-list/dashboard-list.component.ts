import { Component, inject, OnInit, signal, computed } from '@angular/core';
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
            <div class="api-key-row">
              <code class="api-key-value">{{ getApiKeyDisplay(dashboard.apiKey, dashboard.id) }}</code>
              <button class="icon-btn" title="Reveal API Key" (click)="toggleReveal(dashboard.id)">
                <i class="fa-regular" [ngClass]="revealedKeys()[dashboard.id] ? 'fa-eye-slash' : 'fa-eye'"></i>
              </button>
              <button class="icon-btn" title="Copy API Key" (click)="copyApiKey(dashboard.apiKey)">
                <i class="fa-regular fa-clipboard"></i>
              </button>
            </div>
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
      grid-template-columns: auto 1fr 320px 80px;
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

    .api-key-row {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.375rem 0.5rem;
      background: var(--bs-secondary-bg);
      border: 1px solid var(--bs-border-color);
      border-radius: 0.25rem;
      font-size: 0.8rem;
      grid-column: 3;
      min-width: 0;
    }

    .api-key-value {
      font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
      color: var(--bs-body-color);
      background: transparent;
      border: none;
      padding: 0;
      margin: 0;
      user-select: all;
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 24px;
      height: 24px;
      padding: 0;
      background: transparent;
      border: none;
      color: var(--bs-secondary-color);
      cursor: pointer;
      border-radius: 0.25rem;
      transition: all 0.15s ease;
      font-size: 0.75rem;
      flex-shrink: 0;
    }

    .icon-btn:hover {
      background: var(--bs-tertiary-bg);
      color: var(--bs-body-color);
    }

    .icon-btn:active {
      transform: scale(0.95);
    }

    .dashboard-actions {
      display: flex;
      gap: 0.375rem;
      grid-column: 4;
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
        grid-template-columns: auto 1fr 280px 80px;
      }
    }

    @media (max-width: 1024px) {
      .dashboard-item {
        grid-template-columns: auto 1fr 260px 80px;
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

      .api-key-row {
        grid-column: 1;
        width: 100%;
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
  readonly revealedKeys = signal<Record<string, boolean>>({});

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

  toggleReveal(id: string): void {
    this.revealedKeys.update(m => ({ ...m, [id]: !m[id] }));
  }

  getApiKeyDisplay(apiKey: string, id: string): string {
    const revealed = this.revealedKeys()[id];
    if (revealed) return apiKey || '';
    if (!apiKey) return '';
    return apiKey.length > 8 ? `${apiKey.slice(0, 6)}••••` : apiKey.replace(/.(?=.{2})/g, '•');
  }

  async copyApiKey(apiKey: string): Promise<void> {
    const tryClipboardApi = async () => {
      if (!navigator.clipboard || !window.isSecureContext) {
        throw new Error('Clipboard API not available');
      }
      await navigator.clipboard.writeText(apiKey);
    };

    try {
      await tryClipboardApi();
      this.toastService.success('API key copied to clipboard');
      return;
    } catch (err) {
    }

    try {
      const textarea = document.createElement('textarea');
      textarea.value = apiKey;
      textarea.setAttribute('readonly', '');
      textarea.style.position = 'fixed';
      textarea.style.left = '-9999px';
      document.body.appendChild(textarea);
      textarea.select();
      const copied = document.execCommand('copy');
      document.body.removeChild(textarea);

      if (!copied) {
        throw new Error('execCommand copy failed');
      }

      this.toastService.success('API key copied to clipboard');
    } catch (fallbackErr) {
      this.toastService.error('Unable to copy API key');
    }
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
