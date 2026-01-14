import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-dashboard-edit',
  imports: [CommonModule, FormsModule],
  template: `
    <h2>Edit Dashboard</h2>

    @if (isLoading()) {
      <div class="text-center my-5">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (dashboard()) {
      <form (ngSubmit)="onSubmit()">
        <div class="mb-3">
          <label class="form-label">Name</label>
          <input type="text" class="form-control" [(ngModel)]="dashboard()!.name" name="name" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Description</label>
          <input type="text" class="form-control" [(ngModel)]="dashboard()!.description" name="description" />
        </div>
        <div class="mb-3">
          <label class="form-label">API Key</label>
          <div class="input-group">
            <input type="text" class="form-control" [value]="dashboard()!.apiKey" readonly tabindex="-1" style="pointer-events: none;" />
            <button type="button" class="btn btn-outline-secondary" title="Copy API Key" (click)="copyApiKey()">
              <i class="fa-regular fa-clipboard"></i>
            </button>
          </div>
        </div>

        <!-- Home Assistant settings -->
        <h4 class="mt-4">Home Assistant Integration</h4>
        <div class="mb-3">
          <label class="form-label">Host</label>
          <input type="text" class="form-control" [(ngModel)]="dashboard()!.host" name="host" placeholder="https://your-ha-instance.com" />
        </div>
        <div class="mb-3">
          <label class="form-label">Path</label>
          <input type="text" class="form-control" [(ngModel)]="dashboard()!.path" name="path" />
        </div>

        @if (errorMessage()) {
          <div class="alert alert-danger">{{ errorMessage() }}</div>
        }
        @if (successMessage()) {
          <div class="alert alert-success">{{ successMessage() }}</div>
        }

        <div class="d-flex gap-2">
          <button type="submit" class="btn btn-primary" [disabled]="isSaving()">
            {{ isSaving() ? 'Saving...' : 'Save Changes' }}
          </button>
          <button type="button" class="btn btn-secondary" (click)="onCancel()">Cancel</button>
        </div>
      </form>

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
    }
  `
})
export class DashboardEditComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly dashboard = signal<Dashboard | null>(null);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  readonly showCopyToast = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadDashboard(id);
    }
  }

  loadDashboard(id: string): void {
    this.isLoading.set(true);
    this.dashboardService.getDashboard(id).subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        this.isLoading.set(false);
      },
      error: () => {
        this.router.navigate(['/dashboards']);
      }
    });
  }

  onSubmit(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) return;

    this.isSaving.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.dashboardService.updateDashboard(currentDashboard.id, {
      name: currentDashboard.name,
      description: currentDashboard.description,
      host: currentDashboard.host || undefined,
      path: currentDashboard.path || undefined
    }).subscribe({
      next: () => {
        this.successMessage.set('Dashboard updated successfully!');
        this.isSaving.set(false);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Failed to update dashboard.');
        this.isSaving.set(false);
      }
    });
  }

  onCancel(): void {
    this.router.navigate(['/dashboards']);
  }

  copyApiKey(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) return;
    navigator.clipboard.writeText(currentDashboard.apiKey).then(() => {
      this.showCopyToast.set(true);
      setTimeout(() => {
        this.showCopyToast.set(false);
      }, 3000);
    }).catch((err) => {
      console.error('Failed to copy API key:', err);
      alert(`API Key: ${currentDashboard.apiKey}`);
    });
  }
}
