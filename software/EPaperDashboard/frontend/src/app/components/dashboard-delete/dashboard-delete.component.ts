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
    <div class="container mt-5">
      <h2 class="text-center">Delete Dashboard</h2>
      
      @if (isLoading()) {
        <div class="text-center my-5">
          <div class="spinner-border" role="status">
            <span class="visually-hidden">Loading...</span>
          </div>
        </div>
      } @else if (dashboard()) {
        <div class="card w-50 mx-auto">
          <div class="card-body">
            <h5 class="card-title">{{ dashboard()!.name }}</h5>
            <p class="card-text">{{ dashboard()!.description }}</p>
            <p class="text-danger">Are you sure you want to delete this dashboard? This action cannot be undone.</p>
            
            @if (errorMessage()) {
              <div class="alert alert-danger">{{ errorMessage() }}</div>
            }
            
            <div class="d-flex gap-2">
              <button class="btn btn-danger" (click)="onDelete()" [disabled]="isDeleting()">
                {{ isDeleting() ? 'Deleting...' : 'Yes, Delete' }}
              </button>
              <button class="btn btn-secondary" (click)="onCancel()">Cancel</button>
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
