import { Component, inject, OnInit, OnDestroy, signal, ChangeDetectorRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DashboardService } from '../../services/dashboard.service';
import { HomeAssistantService } from '../../services/home-assistant.service';
import { ToastService } from '../../services/toast.service';
import { DialogService } from '../../services/dialog.service';
import { Dashboard } from '../../models/types';
import { ToastContainerComponent } from '../toast-container/toast-container.component';
import { DashboardSelectorDialogComponent } from '../dashboard-selector-dialog/dashboard-selector-dialog.component';

@Component({
  selector: 'app-dashboard-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ToastContainerComponent, DashboardSelectorDialogComponent],
  template: `
    <app-toast-container></app-toast-container>
    <app-dashboard-selector-dialog></app-dashboard-selector-dialog>
    <h2>Edit Dashboard</h2>

    @if (isLoading()) {
      <div class="text-center my-5">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (dashboard()) {
      <form (ngSubmit)="onSubmit()" [formGroup]="dashboardForm">
        <div class="card shadow-sm mb-3">
          <div class="card-body">
            <div class="row g-4">
              <div class="col-12 col-lg-6">
                <div class="mb-3">
                  <label class="form-label fw-semibold">Name</label>
                  <input 
                    type="text" 
                    class="form-control" 
                    formControlName="name"
                    required 
                  />
                </div>
                <div class="mb-3">
                  <label class="form-label fw-semibold">Description</label>
                  <input 
                    type="text" 
                    class="form-control" 
                    formControlName="description"
                  />
                </div>
                <div class="mb-3">
                  <label class="form-label fw-semibold">API Key</label>
                  <div class="input-group">
                    <input type="text" class="form-control" [value]="dashboard()!.apiKey" readonly tabindex="-1" style="pointer-events: none;" />
                    <button type="button" class="btn btn-outline-secondary" title="Copy API Key" (click)="copyApiKey()">
                      <i class="fa-regular fa-clipboard"></i>
                    </button>
                  </div>
                </div>
              </div>

              <div class="col-12 col-lg-6">
                <div class="mb-3">
                  <label class="form-label fw-semibold">Dashboard Host</label>
                  <input 
                    type="text" 
                    class="form-control" 
                    formControlName="host"
                    placeholder="https://your-ha-instance.com" 
                  />
                </div>

                <div class="mb-3">
                  <label class="form-label fw-semibold">Access Token</label>
                  <div class="input-group">
                    <input 
                      type="password" 
                      class="form-control" 
                      formControlName="accessToken"
                      placeholder="Paste token or click Fetch Token..." 
                    />
                    <button 
                      type="button" 
                      class="btn btn-outline-primary" 
                      (click)="authenticateWithHomeAssistant()"
                      [disabled]="isAuthenticating()"
                      title="Authenticate via Home Assistant OAuth"
                    >
                      <i class="fa-solid fa-key"></i> {{ isAuthenticating() ? 'Authenticating...' : 'Fetch' }}
                    </button>
                    @if (dashboard()?.hasAccessToken || dashboardForm.get('accessToken')?.value) {
                      <button 
                        type="button" 
                        class="btn btn-outline-danger" 
                        (click)="clearAccessToken()"
                        title="Clear the access token"
                      >
                        <i class="fa-solid fa-trash"></i> Clear
                      </button>
                    }
                  </div>
                  <small class="form-text text-muted d-block mt-2">
                    @if (dashboard()?.hasAccessToken) {
                      <span class="d-block mt-1 text-success">Token is configured</span>
                    }
                  </small>
                </div>

                <div class="mb-3">
                  <label class="form-label fw-semibold">Dashboard Path</label>
                  <div class="input-group">
                    <input 
                      type="text" 
                      class="form-control" 
                      formControlName="path"
                      id="pathField"
                    />
                    <button 
                      type="button" 
                      class="btn btn-outline-secondary" 
                      (click)="openDashboardSelector()"
                      [disabled]="!dashboardForm.get('host')?.value || (!dashboard()!.hasAccessToken && !dashboardForm.get('accessToken')?.value)"
                    >
                      <i class="fa-solid fa-list"></i> Select
                    </button>
                  </div>
                  <small class="form-text text-muted">Example: lovelace/0 or lovelace/energy</small>
                </div>

                <div class="mb-3">
                  <label class="form-label fw-semibold">Update Times</label>
                  <div class="d-flex align-items-center mb-2">
                    <input 
                      type="time" 
                      class="form-control me-2" 
                      style="width: 150px;"
                      [(ngModel)]="newUpdateTime"
                      [ngModelOptions]="{standalone: true}"
                      name="newUpdateTime"
                    />
                    <button type="button" class="btn btn-outline-primary" (click)="addUpdateTime()">Add</button>
                  </div>
                  <div id="updateTimesList" class="mb-2 d-flex flex-wrap gap-2">
                    @for (time of updateTimes(); track $index) {
                      <span class="badge bg-secondary">
                        {{ time }}
                        <button type="button" class="btn-close btn-close-white ms-2" (click)="removeUpdateTime($index)"></button>
                      </span>
                    } @empty {
                      <span class="text-muted">No update times configured</span>
                    }
                  </div>
                  <small class="form-text text-muted">Add one or more times. Example: 06:00, 12:00, 18:00</small>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="d-flex flex-wrap gap-2">
          <button type="submit" class="btn btn-primary" [disabled]="isSaving() || !dashboardForm.dirty">
            <i class="fa-solid fa-floppy-disk"></i> Save
          </button>
          <button type="button" class="btn btn-secondary" (click)="onCancel()">
            <i class="fa-solid fa-arrow-left"></i> Close
          </button>
          <button type="button" class="btn btn-info" (click)="openPreview()" [disabled]="!dashboardForm.get('host')?.value || !dashboardForm.get('path')?.value || (!dashboard()!.hasAccessToken && !dashboardForm.get('accessToken')?.value)">
            <i class="fa-solid fa-eye"></i> Preview
          </button>
        </div>
      </form>

      <!-- Preview Modal -->
      @if (showPreviewModal()) {
        <div class="position-fixed top-0 start-0 w-100 h-100" style="background-color: rgba(0,0,0,0.5); z-index: 1050; overflow: hidden;">
          <div class="position-absolute top-50 start-50 translate-middle rounded" style="width: 90vw; height: 90vh; max-width: 900px; max-height: 600px; display: flex; flex-direction: column; background-color: var(--bs-body-bg); color: var(--bs-body-color); border: 1px solid var(--bs-border-color);">
            <!-- Modal Header -->
            <div class="d-flex justify-content-between align-items-center p-3" style="border-bottom: 1px solid var(--bs-border-color);">
              <h5 class="mb-0">Dashboard Preview</h5>
              <div class="d-flex gap-2 align-items-center">
                <button type="button" class="btn btn-sm" title="Reload Preview" (click)="openPreview()">
                  <i class="fa-solid fa-arrows-rotate"></i>
                </button>
                <button type="button" class="btn-close" aria-label="Close" (click)="showPreviewModal.set(false)"></button>
              </div>
            </div>
            <!-- Modal Body -->
            <div class="flex-grow-1 overflow-auto p-3 d-flex justify-content-center align-items-flex-start" style="background-color: var(--bs-secondary-bg);">
              @if (previewLoading()) {
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">Loading...</span>
                </div>
              } @else if (previewError()) {
                <div class="alert alert-danger mb-0">{{ previewError() }}</div>
              } @else if (previewImageUrl()) {
                <img [src]="previewImageUrl()" style="max-width: 100%; height: auto; object-fit: contain;" alt="Dashboard preview" />
              }
            </div>
          </div>
        </div>
      }
    }
  `
})
export class DashboardEditComponent implements OnInit, OnDestroy {
  private readonly dashboardService = inject(DashboardService);
  private readonly homeAssistantService = inject(HomeAssistantService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly toastService = inject(ToastService);
  private readonly dialogService = inject(DialogService);

  @ViewChild(DashboardSelectorDialogComponent) dashboardSelectorDialog!: DashboardSelectorDialogComponent;

  readonly dashboard = signal<Dashboard | null>(null);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly isAuthenticating = signal(false);
  readonly updateTimes = signal<string[]>([]);
  readonly showPreviewModal = signal(false);
  readonly previewLoading = signal(false);
  readonly previewError = signal('');
  readonly previewImageUrl = signal('');
  readonly shouldClearAccessToken = signal(false);

  readonly dashboardForm = new FormGroup({
    name: new FormControl('', { validators: Validators.required, nonNullable: true }),
    description: new FormControl('', { nonNullable: true }),
    host: new FormControl('', { nonNullable: true }),
    path: new FormControl('', { nonNullable: true }),
    accessToken: new FormControl('', { nonNullable: true }),
  });
  
  newUpdateTime: string = '';
  private previewObjectUrl: string | null = null;
  private oauthProcessed = false;
  private oauthToken: string | null = null;
  private originalDashboard: Dashboard | null = null;
  private originalUpdateTimes: string[] = [];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    console.log('DashboardEdit init - id:', id);
    
    if (id) {
      // Check for OAuth callback params
      this.route.queryParams.subscribe(params => {
        if (params['access_token'] && params['auth_callback'] === 'true' && !this.oauthProcessed) {
          console.log('✓ OAuth callback detected in query params');
          console.log('  - Token:', params['access_token'].substring(0, 20) + '...');
          this.oauthProcessed = true;
          this.oauthToken = params['access_token'];
        }
      });
      
      this.loadDashboard(id);
    }
  }

  loadDashboard(id: string): void {
    this.isLoading.set(true);
    this.dashboardService.getDashboard(id).subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        this.originalDashboard = JSON.parse(JSON.stringify(dashboard)); // Deep copy for comparison
        
        // Populate the form with dashboard data using reset to properly initialize pristine state
        this.dashboardForm.reset({
          name: dashboard.name || '',
          description: dashboard.description || '',
          host: dashboard.host || '',
          path: dashboard.path || '',
          accessToken: '',
        });
        
        // Convert update times to array for display (normalize string or array)
        if (dashboard.updateTimes) {
          let times: string[] = [];
          if (Array.isArray(dashboard.updateTimes)) {
            times = dashboard.updateTimes.filter(t => !!t);
          } else if (typeof (dashboard as any).updateTimes === 'string') {
            times = (dashboard as any).updateTimes
              .split(',')
              .map((t: string) => t.trim())
              .filter((t: string) => t.length > 0);
          }
          this.updateTimes.set(times);
          this.originalUpdateTimes = [...times]; // Store original for comparison
        } else {
          this.updateTimes.set([]);
          this.originalUpdateTimes = [];
        }
        this.isLoading.set(false);
        
        // Now process OAuth if we have a token
        if (this.oauthToken && !dashboard.hasAccessToken) {
          console.log('✓ Saving OAuth token for dashboard:', id);
          this.saveOAuthToken(id);
        }
        
        // Force change detection to ensure UI updates
        this.cdr.detectChanges();
      },
      error: () => {
        this.router.navigate(['/dashboards']);
      }
    });
  }

  private saveOAuthToken(dashboardId: string): void {
    if (!this.oauthToken) {
      console.warn('No OAuth token to save');
      return;
    }

    const updatePayload = {
      accessToken: this.oauthToken
    };
    
    console.log('  - Sending token to backend...');
    this.dashboardService.updateDashboard(dashboardId, updatePayload).subscribe({
      next: (updated) => {
        console.log('✓✓✓ Token saved successfully!');
        console.log('  - Updated dashboard:', { hasToken: updated.hasAccessToken });
        this.dashboard.set(updated);
        this.toastService.success('Home Assistant token saved successfully!');
        
        // Clean the query params from URL without navigating away
        setTimeout(() => {
          console.log('✓ Clearing OAuth params from URL');
          window.history.replaceState({}, '', `/dashboards/${dashboardId}/edit`);
        }, 500);
        
        this.oauthToken = null;
      },
      error: (error) => {
        console.error('✗ Failed to save token:', error);
        this.toastService.error('Failed to save Home Assistant token. Please try again.');
        this.oauthToken = null;
      }
    });
  }



  addUpdateTime(): void {
    if (this.newUpdateTime) {
      const current = this.updateTimes();
      if (!current.includes(this.newUpdateTime)) {
        const newTimes = [...current, this.newUpdateTime].sort();
        this.updateTimes.set(newTimes);
        this.newUpdateTime = '';
        this.checkUpdateTimesChanged(newTimes);
      }
    }
  }

  removeUpdateTime(index: number): void {
    const current = this.updateTimes();
    const newTimes = current.filter((_, i) => i !== index);
    this.updateTimes.set(newTimes);
    this.checkUpdateTimesChanged(newTimes);
  }

  private checkUpdateTimesChanged(currentTimes: string[]): void {
    // Compare current times with original
    const timesChanged = JSON.stringify(currentTimes.sort()) !== JSON.stringify(this.originalUpdateTimes.sort());
    
    // If times changed, mark form as dirty
    if (timesChanged) {
      this.dashboardForm.markAsDirty();
    }
  }

  authenticateWithHomeAssistant(): void {
    const currentDashboard = this.dashboard();
    const hostValue = this.dashboardForm.get('host')?.value;
    if (!currentDashboard || !hostValue) {
      this.toastService.error('Please enter Home Assistant host first.');
      return;
    }

    console.log('✓ Starting Home Assistant OAuth flow...');
    console.log('  - Host:', hostValue);
    console.log('  - Dashboard ID:', currentDashboard.id);

    this.isAuthenticating.set(true);

    // Call backend to start OAuth flow
    this.homeAssistantService.startAuth(hostValue, currentDashboard.id).subscribe({
      next: (response) => {
        console.log('✓ OAuth URL received, redirecting to Home Assistant...');
        // Redirect to Home Assistant OAuth URL
        window.location.href = response.authUrl;
      },
      error: (error) => {
        console.error('✗ Failed to start authentication:', error);
        this.isAuthenticating.set(false);
        const errorMsg = 'Failed to start authentication: ' + (error.error?.error || error.error?.message || 'Unknown error');
        this.toastService.error(errorMsg);
      }
    });
  }

  openDashboardSelector(): void {
    const currentDashboard = this.dashboard();
    const hostValue = this.dashboardForm.get('host')?.value;
    const accessTokenValue = this.dashboardForm.get('accessToken')?.value;
    if (!currentDashboard || !hostValue || (!currentDashboard.hasAccessToken && !accessTokenValue)) {
      this.toastService.error('Please configure host and access token first.');
      return;
    }

    console.log('Opening dashboard selector for dashboard:', currentDashboard.id);

    // Open dialog with loading state
    this.dashboardSelectorDialog.openWithLoading();

    // Fetch available dashboards from Home Assistant
    this.homeAssistantService.getDashboards(hostValue, currentDashboard.id)
      .subscribe({
        next: (dashboards) => {
          console.log('Fetched raw dashboards:', dashboards);
          
          // Transform dashboards to have url_path property (use id as the path)
          const transformedDashboards = dashboards.map((item: any) => ({
            url_path: item.id,
            title: item.title,
            id: item.id
          }));
          
          console.log('Transformed dashboards for dialog:', transformedDashboards);
          
          // Now show the dashboards and set up the promise
          this.dashboardSelectorDialog.open(transformedDashboards).then((selectedPath) => {
            console.log('Promise resolved with path:', selectedPath);
            if (selectedPath) {
              // Update the form with the selected path
              this.dashboardForm.patchValue({ path: selectedPath });
              this.dashboardForm.markAsDirty();
            }
          });
        },
        error: (error) => {
          console.error('Error fetching dashboards:', error);
          this.dashboardSelectorDialog.setError(error.error?.message || 'Failed to fetch dashboards');
        }
      });
  }

  onSubmit(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) {
      console.warn('onSubmit: no dashboard');
      return;
    }

    if (!this.dashboardForm.valid) {
      this.toastService.error('Please fill in all required fields.');
      return;
    }

    this.isSaving.set(true);

    const formValue = this.dashboardForm.getRawValue();
    const updatePayload: any = {
      name: formValue.name || undefined,
      description: formValue.description || undefined,
      host: formValue.host || undefined,
      path: formValue.path || undefined,
      updateTimes: this.updateTimes().length > 0 ? this.updateTimes() : undefined
    };

    // Handle manually entered token
    if (formValue.accessToken?.trim().length > 0) {
      updatePayload.accessToken = formValue.accessToken;
      console.log('✓ Manual token included in payload');
    }

    // Handle explicit token clear request
    if (this.shouldClearAccessToken()) {
      updatePayload.clearAccessToken = true;
      console.log('✓ Clear token flag set');
      this.shouldClearAccessToken.set(false); // Reset flag after adding to payload
    }

    console.log('onSubmit - current dashboard:', { 
      id: currentDashboard.id,
      name: currentDashboard.name,
      host: currentDashboard.host,
      path: currentDashboard.path,
      hasToken: currentDashboard.hasAccessToken
    });
    console.log('onSubmit - payload being sent:', updatePayload);

    this.dashboardService.updateDashboard(currentDashboard.id, updatePayload).subscribe({
      next: (updated) => {
        console.log('✓ Dashboard saved successfully:', { 
          id: updated.id,
          host: updated.host,
          path: updated.path,
          hasAccessToken: updated.hasAccessToken
        });
        this.dashboard.set(updated);
        // Update form with saved values to reset dirty state
        this.dashboardForm.patchValue({
          name: updated.name || '',
          description: updated.description || '',
          host: updated.host || '',
          path: updated.path || '',
          accessToken: '' // Clear the token field after successful save
        });
        this.dashboardForm.markAsPristine();
        // Update original update times after save
        if (updated.updateTimes && Array.isArray(updated.updateTimes)) {
          this.originalUpdateTimes = [...updated.updateTimes];
        } else {
          this.originalUpdateTimes = [];
        }
        this.toastService.success('Dashboard updated successfully!');
        this.isSaving.set(false);
      },
      error: (error) => {
        console.error('✗ Save error:', error);
        this.toastService.error(error.error?.message || 'Failed to update dashboard.');
        this.isSaving.set(false);
      }
    });
  }

  onCancel(): void {
    if (this.dashboardForm.dirty) {
      this.dialogService.confirm({
        title: 'Unsaved Changes',
        message: 'You have unsaved changes. Are you sure you want to leave without saving?',
        confirmLabel: 'Leave',
        isDangerous: true,
        onConfirm: () => {
          this.router.navigate(['/dashboards']);
        }
      });
    } else {
      this.router.navigate(['/dashboards']);
    }
  }



  async copyApiKey(): Promise<void> {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) return;

    const apiKey = currentDashboard.apiKey;

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
      console.warn('Clipboard API failed, attempting fallback copy', err);
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
      console.error('Fallback copy failed:', fallbackErr);
      this.toastService.error('Unable to copy API key. Showing it instead.');
      alert(`API Key: ${apiKey}`);
    }
  }

  async clearAccessToken(): Promise<void> {
    await this.dialogService.confirm({
      title: 'Clear Access Token',
      message: 'Are you sure you want to clear the Home Assistant access token? The dashboard will no longer be able to render until you authenticate again.',
      confirmLabel: 'Clear Token',
      isDangerous: true,
      onConfirm: () => {
        console.log('✓ User confirmed token clear');
        this.shouldClearAccessToken.set(true);
        this.dashboardForm.markAsDirty();
        this.onSubmit();
      }
    });
  }

  openPreview(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) return;

    // Validate that dashboard has required Home Assistant configuration
    const hostValue = this.dashboardForm.get('host')?.value;
    const pathValue = this.dashboardForm.get('path')?.value;
    const accessTokenValue = this.dashboardForm.get('accessToken')?.value;
    if (!hostValue || !pathValue || (!currentDashboard.hasAccessToken && !accessTokenValue)) {
      this.toastService.error('Preview requires Home Assistant configuration and access token. Please configure Host, Dashboard Path, and add an access token first.');
      return;
    }

    this.showPreviewModal.set(true);
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewImageUrl.set('');

    // Clean up previous preview URL
    if (this.previewObjectUrl) {
      try {
        URL.revokeObjectURL(this.previewObjectUrl);
      } catch (e) {
        // Ignore cleanup errors
      }
      this.previewObjectUrl = null;
    }

    // Debug logging
    console.log('Preview request:', {
      dashboardId: currentDashboard.id,
      host: currentDashboard.host,
      path: currentDashboard.path,
      hasAccessToken: currentDashboard.hasAccessToken,
      apiKey: currentDashboard.apiKey
    });

    // Fetch preview image - match Razor implementation exactly
    const url = `/api/render/original?width=800&height=480&format=png`;

    console.log('Making preview request:', {
      url,
      apiKey: currentDashboard.apiKey,
      headers: { 'X-Api-Key': currentDashboard.apiKey }
    });

    this.http.get(url, {
      headers: { 'X-Api-Key': currentDashboard.apiKey },
      responseType: 'blob'
    }).subscribe({
      next: (blob) => {
        console.log('Preview response received, blob size:', blob.size);
        const imageUrl = URL.createObjectURL(blob);
        this.previewObjectUrl = imageUrl;
        this.previewImageUrl.set(imageUrl);
        this.previewLoading.set(false);
      },
      error: async (error) => {
        console.error('Preview request error:', {
          status: error.status,
          statusText: error.statusText,
          errorType: error.error?.constructor?.name,
          errorSize: error.error instanceof Blob ? error.error.size : 'N/A'
        });
        
        this.previewLoading.set(false);
        const dashboard = this.dashboard();
        let errorMessage = 'Failed to load preview';
        
        // Try to extract error message from blob response
        if (error.error instanceof Blob) {
          try {
            const text = await error.error.text();
            console.log('Error blob text:', text);
            
            // Try to parse as JSON first
            try {
              const json = JSON.parse(text);
              console.log('Error JSON parsed:', json);
              errorMessage = json.title || json.error || json.message || text;
            } catch (jsonError) {
              // Not JSON, use plain text
              console.log('Error is plain text, not JSON');
              errorMessage = text || `HTTP Error ${error.status}`;
            }
          } catch (e) {
            console.log('Failed to read error blob:', e);
            errorMessage = `HTTP Error ${error.status}`;
          }
        } else if (error.status === 404) {
          // Debug: show what config was sent
          const configDebug = dashboard ? `Host: ${dashboard.host || 'MISSING'}, Path: ${dashboard.path || 'MISSING'}, Token: ${dashboard.hasAccessToken ? 'SET' : 'MISSING'}` : 'No dashboard';
          errorMessage = `404 Not Found. Config: [${configDebug}] - Make sure Home Assistant settings are complete and saved.`;
        } else if (error.error && typeof error.error === 'string') {
          errorMessage = error.error;
        } else if (error.error && error.error.error) {
          errorMessage = error.error.error;
        } else if (error.error && error.error.message) {
          errorMessage = error.error.message;
        } else if (error.status) {
          errorMessage = `HTTP Error ${error.status}`;
        }
        
        console.log('Final error message:', errorMessage);
        this.previewError.set(errorMessage);
        this.toastService.error(errorMessage);
      }
    });
  }

  ngOnDestroy(): void {
    // Clean up preview URL on component destroy
    if (this.previewObjectUrl) {
      try {
        URL.revokeObjectURL(this.previewObjectUrl);
      } catch (e) {
        // Ignore cleanup errors
      }
    }
  }
}

