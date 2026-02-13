import { Component, inject, OnInit, OnDestroy, signal, ChangeDetectorRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DashboardService } from '../../services/dashboard.service';
import { HomeAssistantService } from '../../services/home-assistant.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { DialogService } from '../../services/dialog.service';
import { Dashboard } from '../../models/types';
import { ToastContainerComponent } from '../toast-container/toast-container.component';
import { DashboardSelectorDialogComponent } from '../dashboard-selector-dialog/dashboard-selector-dialog.component';
import { RenderedPreviewModalComponent } from '../rendered-preview-modal/rendered-preview-modal.component';

@Component({
  selector: 'app-dashboard-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ToastContainerComponent, DashboardSelectorDialogComponent, RenderedPreviewModalComponent],
  styles: [`
    .btn-group[role="group"] {
      display: grid !important;
      grid-template-columns: 1fr 1fr;
      gap: 0;
    }

    .btn-group[role="group"] .btn {
      flex: none !important;
    }

    .d-flex.align-items-center.gap-3 {
      min-width: 0;
    }

    h2 {
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `],
  template: `
    <app-toast-container></app-toast-container>
    <app-dashboard-selector-dialog></app-dashboard-selector-dialog>

    @if (isLoading()) {
      <div class="text-center my-5">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (dashboard()) {
      <form (ngSubmit)="onSubmit()" [formGroup]="dashboardForm">
        <div class="d-flex justify-content-between align-items-center mb-4">
          <div class="d-flex align-items-center gap-3">
            <button type="button" class="btn btn-secondary" (click)="onCancel()">
              <i class="fa-solid fa-arrow-left"></i> Back
            </button>
            <h2 class="mb-0">Edit Dashboard</h2>
          </div>
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-success" (click)="openPreview()" 
              [disabled]="disablePreviewButton()"
              [title]="previewMode() === 'ssr' ? 'Open custom layout preview' : 'Open Home Assistant dashboard preview'">
              <i class="fa-solid fa-eye"></i> Preview
            </button>
            <button type="submit" class="btn btn-primary" [disabled]="isSaving() || !dashboardForm.dirty">
              <i class="fa-solid fa-floppy-disk"></i> Save
            </button>
          </div>
        </div>
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
                @if (!isHomeAssistantMode()) {
                  <div class="mb-3">
                    <label class="form-label fw-semibold">Dashboard Host</label>
                    <input 
                      type="text" 
                      class="form-control" 
                      formControlName="host"
                      placeholder="https://your-ha-instance.com" 
                    />
                  </div>
                }

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
                      title="Authenticate via Home Assistant"
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
                  <label class="form-label fw-semibold">Rendering Mode</label>
                  <div class="btn-group d-flex" role="group">
                    <input type="radio" class="btn-check" name="previewMode" id="ssrMode" value="ssr" [(ngModel)]="previewModeValue" [ngModelOptions]="{standalone: true}" (change)="onRenderingModeChange()" />
                    <label class="btn btn-outline-secondary flex-grow-1" for="ssrMode">Custom Layout</label>

                    <input type="radio" class="btn-check" name="previewMode" id="haMode" value="homeassistant" [(ngModel)]="previewModeValue" [ngModelOptions]="{standalone: true}" (change)="onRenderingModeChange()" />
                    <label class="btn btn-outline-secondary flex-grow-1" for="haMode">Home Assistant Dashboard</label>
                  </div>
                </div>

                @if (previewModeValue === 'ssr') {
                  <div class="mb-3">
                    <label class="form-label fw-semibold">Layout</label>
                    <button type="button" class="btn btn-success w-100" (click)="openDesigner()">
                      <i class="fa-solid fa-paint-brush"></i> Open Layout Designer
                    </button>
                  </div>
                } @else {
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
                        [disabled]="isHomeAssistantMode() ? false : (!dashboardForm.get('host')?.value || (!dashboard()!.hasAccessToken && !dashboardForm.get('accessToken')?.value))"
                      >
                        <i class="fa-solid fa-list"></i> Select
                      </button>
                    </div>
                    <small class="form-text text-muted">Example: lovelace/0 or lovelace/energy</small>
                  </div>
                }

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

      </form>

      <!-- Preview Modal -->
      <app-rendered-preview-modal
        title="Dashboard Preview"
        [isOpen]="showPreviewModal"
        [isLoading]="previewLoading"
        [error]="previewError"
        [imageUrl]="previewImageUrl"
        (close)="showPreviewModal.set(false)"
        (reload)="openPreview()">
      </app-rendered-preview-modal>
    }
  `
})
export class DashboardEditComponent implements OnInit, OnDestroy {
  private readonly dashboardService = inject(DashboardService);
  private readonly homeAssistantService = inject(HomeAssistantService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly toastService = inject(ToastService);
  private readonly dialogService = inject(DialogService);

  readonly isHomeAssistantMode = this.authService.isHomeAssistantMode;

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
  readonly previewMode = signal<'ssr' | 'homeassistant'>('ssr');

  readonly dashboardForm = new FormGroup({
    name: new FormControl('', { validators: Validators.required, nonNullable: true }),
    description: new FormControl('', { nonNullable: true }),
    host: new FormControl('', { nonNullable: true }),
    path: new FormControl('', { nonNullable: true }),
    accessToken: new FormControl('', { nonNullable: true }),
  });

  newUpdateTime: string = '';
  previewModeValue: 'ssr' | 'homeassistant' = 'ssr';
  private previewObjectUrl: string | null = null;
  private oauthProcessed = false;
  private oauthToken: string | null = null;
  private originalDashboard: Dashboard | null = null;
  private originalUpdateTimes: string[] = [];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id) {
      this.route.queryParams.subscribe(params => {
        if (params['access_token'] && params['auth_callback'] === 'true' && !this.oauthProcessed) {
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
        this.originalDashboard = JSON.parse(JSON.stringify(dashboard));

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
          this.originalUpdateTimes = [...times];
        } else {
          this.updateTimes.set([]);
          this.originalUpdateTimes = [];
        }

        // Load rendering mode preference
        if (dashboard.renderingMode === 'HomeAssistant') {
          this.previewModeValue = 'homeassistant';
        } else {
          this.previewModeValue = 'ssr';
        }
        this.previewMode.set(this.previewModeValue);

        this.isLoading.set(false);

        // Now process OAuth if we have a token
        if (this.oauthToken && !dashboard.hasAccessToken) {
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
      return;
    }

    const updatePayload = {
      accessToken: this.oauthToken
    };

    this.dashboardService.updateDashboard(dashboardId, updatePayload).subscribe({
      next: (updated) => {
        this.dashboard.set(updated);
        this.toastService.success('Home Assistant token saved successfully!');

        // Clean the query params from URL without navigating away
        setTimeout(() => {
          window.history.replaceState({}, '', `/dashboards/${dashboardId}/edit`);
        }, 500);

        this.oauthToken = null;
      },
      error: (error) => {
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
    const timesChanged = JSON.stringify(currentTimes.sort()) !== JSON.stringify(this.originalUpdateTimes.sort());

    if (timesChanged) {
      this.dashboardForm.markAsDirty();
    }
  }

  authenticateWithHomeAssistant(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) {
      return;
    }

    const hostValue = this.dashboardForm.get('host')?.value;
    if (!this.isHomeAssistantMode() && !hostValue) {
      this.toastService.error('Please enter Home Assistant host first.');
      return;
    }

    this.isAuthenticating.set(true);

    this.homeAssistantService.startAuth(hostValue || '', currentDashboard.id).subscribe({
      next: (response: any) => {
        if (response.directAuth) {
          this.isAuthenticating.set(false);
          this.loadDashboard(currentDashboard.id);
          this.toastService.success('Access token fetched successfully!');
        } else {
          this.isAuthenticating.set(false);
          window.location.href = response.authUrl;
        }
      },
      error: (error) => {
        this.isAuthenticating.set(false);
        const errorMsg = 'Failed to start authentication: ' + (error.error?.error || error.error?.message || 'Unknown error');
        this.toastService.error(errorMsg);
      }
    });
  }

  openDashboardSelector(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) {
      this.toastService.error('Dashboard not loaded.');
      return;
    }

    // In Home Assistant mode, skip host/token validation (using supervisor credentials)
    if (!this.isHomeAssistantMode()) {
      const hostValue = this.dashboardForm.get('host')?.value;
      const accessTokenValue = this.dashboardForm.get('accessToken')?.value;
      if (!hostValue || (!currentDashboard.hasAccessToken && !accessTokenValue)) {
        this.toastService.error('Please configure host and access token first.');
        return;
      }
    }

    this.dashboardSelectorDialog.openWithLoading();

    this.homeAssistantService.getDashboards(currentDashboard.id)
      .subscribe({
        next: (dashboards) => {
          const transformedDashboards = dashboards.map((item: any) => ({
            url_path: item.id,
            title: item.title,
            id: item.id
          }));

          this.dashboardSelectorDialog.open(transformedDashboards).then((selectedPath) => {
            if (selectedPath) {
              // Update the form with the selected path
              this.dashboardForm.patchValue({ path: selectedPath });
              this.dashboardForm.markAsDirty();
            }
          });
        },
        error: (error) => {
          this.dashboardSelectorDialog.setError(error.error?.message || 'Failed to fetch dashboards');
        }
      });
  }

  onSubmit(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) {
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
      updateTimes: this.updateTimes().length > 0 ? this.updateTimes() : undefined,
      renderingMode: this.previewModeValue === 'homeassistant' ? 'HomeAssistant' : 'Custom'
    };

    if (formValue.accessToken?.trim().length > 0) {
      updatePayload.accessToken = formValue.accessToken;
    }

    if (this.shouldClearAccessToken()) {
      updatePayload.clearAccessToken = true;
      this.shouldClearAccessToken.set(false); // Reset flag after adding to payload
    }

    this.dashboardService.updateDashboard(currentDashboard.id, updatePayload).subscribe({
      next: (updated) => {
        this.dashboard.set(updated);
        this.dashboardForm.patchValue({
          name: updated.name || '',
          description: updated.description || '',
          host: updated.host || '',
          path: updated.path || '',
          accessToken: '' // Clear the token field after successful save
        });
        this.dashboardForm.markAsPristine();
        if (updated.updateTimes && Array.isArray(updated.updateTimes)) {
          this.originalUpdateTimes = [...updated.updateTimes];
        } else {
          this.originalUpdateTimes = [];
        }
        this.toastService.success('Dashboard updated successfully!');
        this.isSaving.set(false);
      },
      error: (error) => {
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

  openDesigner(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.router.navigate(['/dashboards', id, 'designer']);
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
      // Clipboard API failed, attempt fallback
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
        this.shouldClearAccessToken.set(true);
        this.dashboardForm.markAsDirty();
        this.onSubmit();
      }
    });
  }

  onRenderingModeChange(): void {
    this.dashboardForm.markAsDirty();
  }

  disablePreviewButton(): boolean {
    const mode = this.previewMode();
    if (mode === 'ssr') {
      return !this.dashboard()?.layoutConfig;
    } else {
      const hostValue = this.dashboardForm.get('host')?.value;
      const pathValue = this.dashboardForm.get('path')?.value;
      const currentDashboard = this.dashboard();
      const accessTokenValue = this.dashboardForm.get('accessToken')?.value;
      return !hostValue || !pathValue || (!currentDashboard?.hasAccessToken && !accessTokenValue);
    }
  }

  openPreview(): void {
    this.previewMode.set(this.previewModeValue);
    
    if (this.previewMode() === 'ssr') {
      this.openSsrPreview();
    } else {
      this.openHomeAssistantPreview();
    }
  }

  private openSsrPreview(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard?.layoutConfig) {
      this.toastService.error('Please configure the dashboard layout in the designer first');
      return;
    }

    this.showPreviewModal.set(true);
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewImageUrl.set('');

    if (this.previewObjectUrl) {
      try {
        URL.revokeObjectURL(this.previewObjectUrl);
      } catch (e) {}
      this.previewObjectUrl = null;
    }

    const url = `/api/dashboards/${currentDashboard.id}/render-image?format=png`;

    this.http.get(url, {
      responseType: 'blob'
    }).subscribe({
      next: (blob) => {
        const imageUrl = URL.createObjectURL(blob);
        this.previewObjectUrl = imageUrl;
        this.previewImageUrl.set(imageUrl);
        this.previewLoading.set(false);
      },
      error: async (error) => {
        this.previewLoading.set(false);
        let errorMessage = 'Failed to load SSR preview';

        if (error.error instanceof Blob) {
          try {
            const text = await error.error.text();
            try {
              const json = JSON.parse(text);
              errorMessage = json.title || json.error || json.message || text;
            } catch (jsonError) {
              errorMessage = text || `HTTP Error ${error.status}`;
            }
          } catch (e) {
            errorMessage = `HTTP Error ${error.status}`;
          }
        } else if (error.status) {
          errorMessage = `HTTP Error ${error.status}`;
        }

        this.previewError.set(errorMessage);
        this.toastService.error(errorMessage);
      }
    });
  }

  private openHomeAssistantPreview(): void {
    const currentDashboard = this.dashboard();
    if (!currentDashboard) return;

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

    if (this.previewObjectUrl) {
      try {
        URL.revokeObjectURL(this.previewObjectUrl);
      } catch (e) {}
      this.previewObjectUrl = null;
    }

    const url = `/api/render/original?width=800&height=480&format=png`;

    this.http.get(url, {
      headers: { 'X-Api-Key': currentDashboard.apiKey },
      responseType: 'blob'
    }).subscribe({
      next: (blob) => {
        const imageUrl = URL.createObjectURL(blob);
        this.previewObjectUrl = imageUrl;
        this.previewImageUrl.set(imageUrl);
        this.previewLoading.set(false);
      },
      error: async (error) => {
        this.previewLoading.set(false);
        const dashboard = this.dashboard();
        let errorMessage = 'Failed to load Home Assistant preview';

        if (error.error instanceof Blob) {
          try {
            const text = await error.error.text();
            try {
              const json = JSON.parse(text);
              errorMessage = json.title || json.error || json.message || text;
            } catch (jsonError) {
              errorMessage = text || `HTTP Error ${error.status}`;
            }
          } catch (e) {
            errorMessage = `HTTP Error ${error.status}`;
          }
        } else if (error.status === 404) {
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

