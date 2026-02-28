import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { DashboardOrientation, DashboardSizePreset, DASHBOARD_SIZE_PRESETS, DEFAULT_DASHBOARD_SIZE } from '../../models/types';

@Component({
  selector: 'app-dashboard-create',
  standalone: true,
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
        <div class="mb-3">
          <label class="form-label">Screen Size</label>
          <select class="form-select" [(ngModel)]="selectedSizeIndex" name="screenSize">
            @for (size of sizePresets; track size.label; let i = $index) {
              <option [ngValue]="i">{{ size.label }}</option>
            }
          </select>
        </div>
        <div class="mb-3">
          <label class="form-label">Orientation</label>
          <div class="btn-group d-flex" role="group">
            <input type="radio" class="btn-check" name="orientation" id="createLandscape" value="Landscape" [(ngModel)]="orientation" />
            <label class="btn btn-outline-secondary flex-grow-1" for="createLandscape">
              <i class="fa-solid fa-display"></i> Landscape
            </label>
            <input type="radio" class="btn-check" name="orientation" id="createPortrait" value="Portrait" [(ngModel)]="orientation" />
            <label class="btn btn-outline-secondary flex-grow-1" for="createPortrait">
              <i class="fa-solid fa-mobile-screen"></i> Portrait
            </label>
          </div>
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
  orientation: DashboardOrientation = 'Landscape';
  sizePresets: DashboardSizePreset[] = DASHBOARD_SIZE_PRESETS;
  selectedSizeIndex = 0;
  readonly errorMessage = signal('');
  readonly isLoading = signal(false);

  get selectedSize(): DashboardSizePreset {
    return this.sizePresets[this.selectedSizeIndex] ?? DEFAULT_DASHBOARD_SIZE;
  }

  onSubmit(): void {
    if (!this.name) {
      this.errorMessage.set('Name is required.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');

    const size = this.selectedSize;
    this.dashboardService.createDashboard({
      name: this.name,
      description: this.description || undefined,
      orientation: this.orientation,
      screenWidth: size.width,
      screenHeight: size.height
    }).subscribe({
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
