import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-dashboard-delete',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="app-page">
      <div class="app-page-header">
        <div>
          <h1 class="app-page-title">Delete dashboard</h1>
          <p class="app-page-description">Review the dashboard before permanently deleting it.</p>
        </div>
      </div>
      
      @if (isLoading()) {
        <div class="app-loading-state">
          <div class="spinner-border" role="status">
            <span class="visually-hidden">Loading dashboard</span>
          </div>
        </div>
      } @else if (dashboard()) {
        <div class="app-form-shell app-form-card">
          <div>
            <h2 class="app-section-title">{{ dashboard()!.name }}</h2>
            <p class="card-text">{{ dashboard()!.description }}</p>
            <p class="text-danger">Are you sure you want to delete this dashboard? This action cannot be undone.</p>
            
            @if (errorMessage()) {
              <div class="alert alert-danger">{{ errorMessage() }}</div>
            }
            
            <div class="d-flex gap-2">
              <button type="button" class="btn btn-danger" (click)="onDelete()" [disabled]="isDeleting()">
                @if (isDeleting()) {
                  <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                }
                Delete dashboard
              </button>
              <button type="button" class="btn btn-outline-secondary" (click)="onCancel()">Cancel</button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class DashboardDeleteComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly dashboard = signal<Dashboard | null>(null);
  readonly isLoading = signal(false);
  readonly isDeleting = signal(false);
  readonly errorMessage = signal('');

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

  onDelete(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) return;

    this.isDeleting.set(true);
    this.errorMessage.set('');

    this.dashboardService.deleteDashboard(currentDashboard.id).subscribe({
      next: () => {
        this.router.navigate(['/dashboards']);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Failed to delete dashboard.');
        this.isDeleting.set(false);
      }
    });
  }

  onCancel(): void {
    this.router.navigate(['/dashboards']);
  }
}
