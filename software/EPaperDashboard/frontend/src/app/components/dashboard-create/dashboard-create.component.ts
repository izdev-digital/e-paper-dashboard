import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';

@Component({
  selector: 'app-dashboard-create',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container mt-5">
      <h2 class="text-center">Create Dashboard</h2>
      <form (ngSubmit)="onSubmit()" class="w-50 mx-auto">
        <div class="mb-3">
          <label class="form-label">Name</label>
          <input type="text" class="form-control" [(ngModel)]="name" name="name" required>
        </div>
        <div class="mb-3">
          <label class="form-label">Description (optional)</label>
          <input type="text" class="form-control" [(ngModel)]="description" name="description">
        </div>
        @if (errorMessage()) {
          <div class="alert alert-danger">{{ errorMessage() }}</div>
        }
        <div class="d-flex gap-2">
          <button type="submit" class="btn btn-primary flex-grow-1" [disabled]="isLoading()">
            {{ isLoading() ? 'Creating...' : 'Create' }}
          </button>
          <button type="button" class="btn btn-secondary" (click)="onCancel()">Cancel</button>
        </div>
      </form>
    </div>
  `
})
export class DashboardCreateComponent {
  private readonly dashboardService = inject(DashboardService);
  private readonly router = inject(Router);

  name = '';
  description = '';
  readonly errorMessage = signal('');
  readonly isLoading = signal(false);

  onSubmit(): void {
    if (!this.name) {
      this.errorMessage.set('Name is required.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');

    this.dashboardService.createDashboard({ name: this.name, description: this.description || undefined }).subscribe({
      next: () => {
        this.router.navigate(['/dashboards']);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Failed to create dashboard.');
        this.isLoading.set(false);
      }
    });
  }

  onCancel(): void {
    this.router.navigate(['/dashboards']);
  }
}
