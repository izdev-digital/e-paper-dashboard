import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { HomeAssistantService } from '../../services/home-assistant.service';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-dashboard-edit',
  standalone: true,
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
        <fieldset class="border rounded p-3 mb-3">
          <legend class="w-auto px-2" style="font-size:1.1em;">Home Assistant Dashboard</legend>
          
          <div class="mb-3">
            <label class="form-label">Dashboard Host</label>
            <input type="text" class="form-control" [(ngModel)]="dashboard()!.host" name="host" placeholder="https://your-ha-instance.com" />
          </div>

          <div class="mb-3">
            <label class="form-label">Access Token</label>
            <div class="input-group">
              <input 
                type="text" 
                class="form-control" 
                [(ngModel)]="dashboard()!.accessToken" 
                name="accessToken" 
                id="accessTokenField"
                placeholder="Enter token to update..." 
              />
              <button 
                type="button" 
                class="btn btn-outline-primary" 
                (click)="authenticateWithHomeAssistant()"
                [disabled]="isAuthenticating()"
              >
                <i class="fa-solid fa-key"></i> {{ isAuthenticating() ? 'Authenticating...' : 'Fetch Token' }}
              </button>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Dashboard Path</label>
            <div class="input-group">
              <input 
                type="text" 
                class="form-control" 
                [(ngModel)]="dashboard()!.path" 
                name="path" 
                id="pathField"
              />
              <button 
                type="button" 
                class="btn btn-outline-secondary" 
                (click)="openDashboardSelector()"
                [disabled]="!dashboard()!.host || !dashboard()!.accessToken"
              >
                <i class="fa-solid fa-list"></i> Select
              </button>
            </div>
            <small class="form-text text-muted">Example: lovelace/0 or lovelace/energy</small>
          </div>

          <div class="mb-3">
            <label class="form-label">Update Times</label>
            <div class="d-flex align-items-center mb-2">
              <input 
                type="time" 
                class="form-control me-2" 
                style="width: 150px;"
                [(ngModel)]="newUpdateTime"
                name="newUpdateTime"
              />
              <button type="button" class="btn btn-outline-primary" (click)="addUpdateTime()">Add</button>
            </div>
            <div id="updateTimesList" class="mb-2">
              @for (time of updateTimes(); track $index) {
                <span class="badge bg-secondary me-2 mb-2">
                  {{ time }}
                  <button type="button" class="btn-close btn-close-white ms-2" (click)="removeUpdateTime($index)"></button>
                </span>
              }
            </div>
            <small class="form-text text-muted">Add one or more times. Example: 06:00, 12:00, 18:00</small>
          </div>
        </fieldset>

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
  private readonly homeAssistantService = inject(HomeAssistantService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly dashboard = signal<Dashboard | null>(null);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly isAuthenticating = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  readonly showCopyToast = signal(false);
  readonly updateTimes = signal<string[]>([]);
  newUpdateTime: string = '';

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
        // Convert update times to array for display
        if (dashboard.updateTimes && Array.isArray(dashboard.updateTimes)) {
          this.updateTimes.set(dashboard.updateTimes);
        }
        this.isLoading.set(false);
      },
      error: () => {
        this.router.navigate(['/dashboards']);
      }
    });
  }

  addUpdateTime(): void {
    if (this.newUpdateTime) {
      const current = this.updateTimes();
      if (!current.includes(this.newUpdateTime)) {
        this.updateTimes.set([...current, this.newUpdateTime].sort());
        this.newUpdateTime = '';
      }
    }
  }

  removeUpdateTime(index: number): void {
    const current = this.updateTimes();
    this.updateTimes.set(current.filter((_, i) => i !== index));
  }

  authenticateWithHomeAssistant(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard || !currentDashboard.host) {
      this.errorMessage.set('Please enter Home Assistant host first.');
      return;
    }

    this.isAuthenticating.set(true);
    this.errorMessage.set('');

    // Call backend to start OAuth flow
    this.homeAssistantService.startAuth(currentDashboard.host, currentDashboard.id).subscribe({
      next: (response) => {
        // Redirect to Home Assistant OAuth URL
        window.location.href = response.authUrl;
      },
      error: (error) => {
        this.isAuthenticating.set(false);
        this.errorMessage.set('Failed to start authentication: ' + (error.error?.error || error.error?.message || 'Unknown error'));
      }
    });
  }

  openDashboardSelector(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard || !currentDashboard.host || !currentDashboard.accessToken) {
      this.errorMessage.set('Please configure host and access token first.');
      return;
    }

    // Fetch available dashboards from Home Assistant
    this.homeAssistantService.getDashboards(currentDashboard.host, currentDashboard.id)
      .subscribe({
        next: (dashboards) => {
          // Simple selection: for now just show a dialog
          const paths = dashboards.map((d: any) => d.url_path).join('\n');
          const selected = prompt('Available dashboards:\n\n' + paths + '\n\nEnter dashboard path:');
          if (selected) {
            currentDashboard.path = selected;
            this.dashboard.set({ ...currentDashboard });
          }
        },
        error: (error) => {
          this.errorMessage.set('Failed to fetch dashboards: ' + (error.error?.message || 'Unknown error'));
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
      path: currentDashboard.path || undefined,
      accessToken: currentDashboard.accessToken || undefined,
      updateTimes: this.updateTimes().length > 0 ? this.updateTimes() : undefined
    }).subscribe({
      next: () => {
        this.successMessage.set('Dashboard updated successfully!');
        this.isSaving.set(false);
        setTimeout(() => {
          this.router.navigate(['/dashboards']);
        }, 1500);
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

