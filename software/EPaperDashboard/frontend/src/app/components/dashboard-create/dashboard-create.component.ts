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
    <div class="app-page py-4">
      <div class="app-form-shell">
        <div class="app-page-header">
          <div>
            <h1 class="app-page-title">Create dashboard</h1>
            <p class="app-page-description">Choose the target display. You can add and arrange widgets next.</p>
          </div>
        </div>
        <form (ngSubmit)="onSubmit()" class="app-form-card" novalidate>
        <div class="mb-3">
          <label class="form-label fw-semibold" for="dashboardName">Name</label>
          <input id="dashboardName" type="text" class="form-control" [(ngModel)]="name" name="name"
            autocomplete="off" required autofocus>
        </div>
        <div class="mb-3">
          <label class="form-label fw-semibold" for="dashboardDescription">Description <span class="text-muted fw-normal">(optional)</span></label>
          <input id="dashboardDescription" type="text" class="form-control" [(ngModel)]="description" name="description">
        </div>
        <div class="mb-3">
          <label class="form-label fw-semibold" for="dashboardScreenSize">Screen size</label>
          <select id="dashboardScreenSize" class="form-select" [(ngModel)]="selectedSizeIndex" name="screenSize">
            @for (size of sizePresets; track size.label; let i = $index) {
              <option [ngValue]="i">{{ size.label }}</option>
            }
          </select>
        </div>
        <div class="mb-3">
          <span class="form-label fw-semibold d-block" id="dashboardOrientationLabel">Orientation</span>
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
          <div class="alert alert-danger" role="alert">{{ errorMessage() }}</div>
        }
        <div class="d-flex flex-column-reverse flex-sm-row justify-content-end gap-2">
          <button type="button" class="btn btn-outline-secondary" (click)="onCancel()">Cancel</button>
          <button type="submit" class="btn btn-primary flex-grow-1" [disabled]="isLoading()">
            @if (isLoading()) {
              <span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>
            }
            {{ isLoading() ? 'Creating...' : 'Create and open designer' }}
          </button>
        </div>
        </form>
      </div>
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
      next: (dashboard) => {
        this.router.navigate(['/dashboards', dashboard.id, 'designer']);
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
