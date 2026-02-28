import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DomSanitizer } from '@angular/platform-browser';
import { marked } from 'marked';
import { DeviceService, Device } from '../../services/device.service';
import { DashboardService } from '../../services/dashboard.service';
import { FirmwareService } from '../../services/firmware.service';
import { AuthService } from '../../services/auth.service';
import { DialogService } from '../../services/dialog.service';
import { ToastService } from '../../services/toast.service';
import { ClipboardService } from '../../services/clipboard.service';
import { ToastContainerComponent } from '../toast-container/toast-container.component';
import { SearchableSelectComponent, SelectOption } from '../searchable-select/searchable-select.component';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-device-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, ToastContainerComponent, SearchableSelectComponent],
  styles: [`
    .device-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-bottom: 2rem;
    }

    .device-card {
      padding: 1rem 1.25rem;
      background: var(--bs-body-bg);
      border: 1px solid var(--bs-border-color);
      border-radius: 0.375rem;
      transition: all 0.15s ease;
    }

    .device-card:hover {
      background: var(--bs-secondary-bg);
      border-color: var(--bs-primary);
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
    }

    .device-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.5rem;
    }

    .device-name {
      font-size: 1.1rem;
      font-weight: 600;
      margin: 0;
    }

    .device-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      font-size: 0.85rem;
      color: var(--bs-secondary-color);
    }

    .device-meta-item {
      display: flex;
      align-items: center;
      gap: 0.375rem;
    }

    .dashboard-select {
      max-width: 280px;
      font-size: 0.85rem;
    }

    @media (max-width: 768px) {
      .device-header {
        flex-direction: column;
        align-items: flex-start;
        gap: 0.5rem;
      }

      .device-meta {
        flex-direction: column;
        gap: 0.375rem;
      }

      .dashboard-select {
        max-width: 100%;
        width: 100%;
      }
    }

    .release-notes {
      max-height: 200px;
      overflow-y: auto;
      color: var(--bs-secondary-color);
    }

    .release-notes :first-child {
      margin-top: 0;
    }

    .release-notes :last-child {
      margin-bottom: 0;
    }

    .release-notes h1, .release-notes h2, .release-notes h3 {
      font-size: 0.95rem;
      font-weight: 600;
      margin: 0.5rem 0 0.25rem;
    }

    .release-notes p {
      margin: 0.25rem 0;
    }

    .release-notes ul, .release-notes ol {
      margin: 0.25rem 0;
      padding-left: 1.25rem;
    }

    .release-notes code {
      font-size: 0.8rem;
      padding: 0.1rem 0.3rem;
      background: var(--bs-secondary-bg);
      border-radius: 0.2rem;
    }

    .release-notes a {
      color: var(--bs-primary);
    }
  `],
  template: `
    <app-toast-container></app-toast-container>
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h1 class="mb-0">Devices</h1>
      @if (!isPairingActive()) {
        <button class="btn btn-primary" (click)="startPairing()" [disabled]="isStartingPairing()">
          <i class="fa-solid fa-plus"></i>
          {{ isStartingPairing() ? 'Starting...' : 'Pair New Device' }}
        </button>
      }
    </div>

    @if (isPairingActive()) {
      <div class="alert alert-info mb-4">
        <div class="d-flex flex-column gap-2">
          <div class="d-flex justify-content-between align-items-start">
            <div>
              <div class="mb-2">
                <strong>Pairing Code:</strong>
              </div>
              <div class="d-flex align-items-center gap-2">
                <div class="fs-3 font-monospace fw-bold text-primary">{{ pairingCode() }}</div>
                <button type="button" class="btn btn-sm btn-outline-primary" (click)="copyPairingCode()" title="Copy to clipboard">
                  <i class="fa-solid" [ngClass]="pairingCodeCopied() ? 'fa-check' : 'fa-copy'"></i>
                </button>
              </div>
            </div>
            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="cancelPairing()">
              <i class="fa-solid fa-times"></i> Cancel
            </button>
          </div>

          @if (pairingStatus() === 'pending') {
            <div class="text-muted small">
              <div><i class="fa-solid fa-clock"></i> Expires in {{ pairingTimeRemaining() }} seconds</div>
              <div><i class="fa-solid fa-info-circle"></i> Enter this code on your device web interface</div>
            </div>
          }

          @if (pairingStatus() === 'awaiting_confirmation' || pairingStatus() === 'confirming') {
            <div class="mt-2 p-3 border rounded bg-body-tertiary">
              <div class="mb-2">
                <strong><i class="fa-solid fa-shield-halved"></i> Confirm Pairing</strong>
              </div>
              @if (pairingDeviceIdentifier()) {
                <div class="mb-2 small">
                  <i class="fa-solid fa-fingerprint"></i> Device: <code>{{ pairingDeviceIdentifier() }}</code>
                </div>
              }
              <div class="mb-2 small text-muted">
                Verify this PIN matches the one shown on your device screen:
              </div>
              <div class="fs-2 font-monospace fw-bold text-danger text-center my-3">{{ pairingConfirmationPin() }}</div>
              <button class="btn btn-success w-100"
                (click)="confirmPairing()"
                [disabled]="pairingStatus() === 'confirming'">
                <i class="fa-solid" [ngClass]="pairingStatus() === 'confirming' ? 'fa-spinner fa-spin' : 'fa-check'"></i>
                {{ pairingStatus() === 'confirming' ? 'Confirming...' : 'PINs Match - Confirm Pairing' }}
              </button>
            </div>
          }

          @if (pairingStatus() === 'pending') {
            <div class="text-muted small">
              <i class="fa-solid fa-spinner fa-spin"></i> Waiting for device to submit pairing code...
            </div>
          }
        </div>
      </div>
    }

    @if (isLoading()) {
      <div class="text-center my-5">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (devices().length > 0) {
      <div class="device-list">
        @for (device of devices(); track device.id) {
          <div class="device-card">
            <div class="device-header">
              <h5 class="device-name">{{ device.name }}</h5>
              <div class="d-flex gap-2">
                <button type="button" class="btn btn-sm btn-outline-danger" (click)="removeDevice(device)" title="Remove device">
                  <i class="fa-solid fa-trash"></i>
                </button>
              </div>
            </div>
            <div class="device-meta">
              <div class="device-meta-item">
                <i class="fa-solid fa-fingerprint"></i>
                <code class="small">{{ device.deviceIdentifier }}</code>
              </div>
              @if (device.firmwareVersion) {
                <div class="device-meta-item">
                  <i class="fa-solid fa-microchip"></i>
                  <span>v{{ device.firmwareVersion }}</span>
                  @if (firmwareInfo()?.version) {
                    @if (!isVersionLower(device.firmwareVersion!, firmwareInfo()!.version!)) {
                      <i class="fa-solid fa-circle-check text-success" title="Up to date"></i>
                    } @else {
                      <i class="fa-solid fa-circle-arrow-up text-warning" title="Update available (latest: v{{ firmwareInfo()!.version }})"></i>
                    }
                  }
                </div>
              }
              @if (device.lastSeenAt) {
                <div class="device-meta-item">
                  <i class="fa-regular fa-clock"></i>
                  <span>Last seen: {{ device.lastSeenAt | date:'short' }}</span>
                </div>
              }
              <div class="device-meta-item">
                <i class="fa-solid fa-calendar"></i>
                <span>Paired: {{ device.pairedAt | date:'mediumDate' }}</span>
              </div>
              @if (device.screenWidth && device.screenHeight) {
                <div class="device-meta-item">
                  <i class="fa-solid fa-expand"></i>
                  <span>Screen: {{ device.screenWidth }}×{{ device.screenHeight }}</span>
                </div>
              }
            </div>
            <div class="mt-2 d-flex align-items-center gap-2">
              <i class="fa-solid fa-display text-muted"></i>
              <label class="small text-muted mb-0">Dashboard:</label>
              <app-searchable-select
                class="dashboard-select"
                [options]="compatibleDashboardOptions(device)"
                [value]="device.dashboardId || ''"
                emptyLabel="— No dashboard assigned —"
                searchPlaceholder="Search dashboards..."
                (selectionChange)="assignDashboard(device, $event)"
              ></app-searchable-select>
            </div>
          </div>
        }
      </div>
    } @else {
      <div class="alert alert-info">
        <i class="fa-solid fa-info-circle"></i> No devices paired yet. Click "Pair New Device" to get started.
      </div>
    }

    <!-- Firmware Updates Section -->
    @if (devices().length > 0) {
      <div class="card shadow-sm mt-4">
        <div class="card-body">
          <h5 class="card-title mb-3"><i class="fa-solid fa-microchip me-2"></i>Firmware Updates</h5>
          @if (isFirmwareLoading()) {
            <div class="text-center py-2">
              <div class="spinner-border spinner-border-sm" role="status">
                <span class="visually-hidden">Checking firmware...</span>
              </div>
            </div>
          } @else if (firmwareInfo()) {
            <div class="card border-secondary-subtle">
              <div class="card-body py-2 px-3">
                @if (firmwareInfo()!.version) {
                  <div class="d-flex justify-content-between align-items-center mb-1">
                    <span class="small fw-semibold">Latest Available</span>
                    <span class="badge bg-primary">v{{ firmwareInfo()!.version }}</span>
                  </div>
                  @if (firmwareInfo()!.publishedAt) {
                    <div class="small text-muted mb-1">
                      Released: {{ firmwareInfo()!.publishedAt | date:'mediumDate' }}
                    </div>
                  }
                  @if (firmwareInfo()!.hasDownload) {
                    <div class="small text-success mb-1">
                      <i class="fa-solid fa-circle-check"></i> Firmware binary available for OTA
                    </div>
                  }
                  @if (deviceUpdateSummary(); as summary) {
                    <div class="small mb-1">
                      @if (summary.upToDate === summary.total) {
                        <span class="text-success"><i class="fa-solid fa-check-double"></i> All {{ summary.total }} device(s) up to date</span>
                      } @else {
                        <span class="text-warning"><i class="fa-solid fa-rotate"></i> {{ summary.upToDate }}/{{ summary.total }} device(s) on v{{ summary.latest }}</span>
                        @if (summary.outdated > 0) {
                          <span class="text-muted"> · {{ summary.outdated }} outdated</span>
                        }
                        @if (summary.unknown > 0) {
                          <span class="text-muted"> · {{ summary.unknown }} unknown</span>
                        }
                      }
                    </div>
                  }
                  @if (firmwareInfo()!.releaseNotes) {
                    <details class="mt-1">
                      <summary class="small text-muted" style="cursor:pointer">Release Notes</summary>
                      <div class="release-notes small mt-1" [innerHTML]="renderMarkdown(firmwareInfo()!.releaseNotes!)"></div>
                    </details>
                  }
                } @else {
                  <div class="small text-muted">
                    <i class="fa-solid fa-info-circle"></i> {{ firmwareInfo()!.message || 'No firmware release info available' }}
                  </div>
                }
              </div>
            </div>
          } @else if (firmwareError()) {
            <div class="small text-danger">
              <i class="fa-solid fa-exclamation-circle"></i> {{ firmwareError() }}
            </div>
          }
          <button type="button" class="btn btn-outline-secondary btn-sm w-100 mt-2"
            (click)="refreshFirmware()" [disabled]="isFirmwareLoading()">
            <i class="fa-solid fa-rotate"></i>
            {{ isFirmwareLoading() ? 'Checking...' : 'Check for Updates' }}
          </button>
        </div>
      </div>
    }
  `
})
export class DeviceListComponent implements OnInit, OnDestroy {
  private readonly deviceService = inject(DeviceService);
  private readonly dashboardService = inject(DashboardService);
  private readonly firmwareService = inject(FirmwareService);
  private readonly authService = inject(AuthService);
  private readonly dialogService = inject(DialogService);
  private readonly toastService = inject(ToastService);
  private readonly clipboardService = inject(ClipboardService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly devices = signal<Device[]>([]);
  readonly dashboards = signal<Dashboard[]>([]);
  readonly isLoading = signal(false);

  readonly isPairingActive = signal(false);
  readonly pairingCode = signal('');
  readonly pairingConfirmationPin = signal('');
  readonly pairingDeviceIdentifier = signal('');
  readonly pairingStatus = signal<'pending' | 'awaiting_confirmation' | 'confirming' | 'confirmed'>('pending');
  readonly pairingTimeRemaining = signal(0);
  readonly isStartingPairing = signal(false);
  readonly pairingCodeCopied = signal(false);

  readonly firmwareInfo = this.firmwareService.firmwareInfo;
  readonly isFirmwareLoading = this.firmwareService.isLoading;
  readonly firmwareError = this.firmwareService.error;

  readonly dashboardOptions = computed<SelectOption[]>(() =>
    this.dashboards().map(d => ({ value: d.id, label: d.name }))
  );

  compatibleDashboardOptions(device: Device): SelectOption[] {
    const allDashboards = this.dashboards();
    if (!device.screenWidth || !device.screenHeight) {
      return allDashboards.map(d => ({ value: d.id, label: d.name }));
    }
    return allDashboards
      .filter(d => d.screenWidth === device.screenWidth && d.screenHeight === device.screenHeight)
      .map(d => ({ value: d.id, label: d.name }));
  }

  readonly deviceUpdateSummary = computed(() => {
    const fw = this.firmwareInfo();
    const devs = this.devices();
    if (!fw?.version || devs.length === 0) return null;
    const latest = fw.version;
    const upToDate = devs.filter(d => d.firmwareVersion && !this.isVersionLower(d.firmwareVersion, latest)).length;
    const outdated = devs.filter(d => d.firmwareVersion && this.isVersionLower(d.firmwareVersion, latest)).length;
    const unknown = devs.filter(d => !d.firmwareVersion).length;
    return { total: devs.length, upToDate, outdated, unknown, latest };
  });

  private pairingTimer: any = null;
  private pairingStatusTimer: any = null;
  private pairingExpiresAt: Date | null = null;

  ngOnInit(): void {
    if (this.authService.isAuthReady()) {
      this.loadData();
    } else {
      const checkInterval = setInterval(() => {
        if (this.authService.isAuthReady()) {
          clearInterval(checkInterval);
          this.loadData();
        }
      }, 10);
    }
  }

  private loadData(): void {
    this.loadDevices();
    this.loadDashboards();
    this.firmwareService.loadLatestFirmware();
  }

  loadDevices(): void {
    this.isLoading.set(true);
    this.deviceService.getDevices().subscribe({
      next: (devices) => {
        this.devices.set(devices);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toastService.error('Failed to load devices');
      }
    });
  }

  loadDashboards(): void {
    this.dashboardService.getDashboards().subscribe({
      next: (dashboards) => {
        this.dashboards.set(dashboards);
      },
      error: () => {
        this.toastService.error('Failed to load dashboards');
      }
    });
  }

  assignDashboard(device: Device, dashboardId: string): void {
    this.deviceService.updateDevice(device.id, {
      dashboardId: dashboardId
    }).subscribe({
      next: (updated) => {
        this.devices.update(list =>
          list.map(d => d.id === device.id ? updated : d)
        );
        if (dashboardId) {
          const dashboard = this.dashboards().find(d => d.id === dashboardId);
          this.toastService.success(`Device assigned to "${dashboard?.name || 'dashboard'}"`);
        } else {
          this.toastService.success('Dashboard unassigned from device');
        }
      },
      error: () => {
        this.toastService.error('Failed to update device');
      }
    });
  }

  removeDevice(device: Device): void {
    this.dialogService.confirm({
      title: 'Remove Device',
      message: `Are you sure you want to remove device "${device.name}"? The device will need to be paired again.`,
      confirmLabel: 'Remove',
      isDangerous: true,
      onConfirm: () => {
        this.deviceService.deleteDevice(device.id).subscribe({
          next: () => {
            this.devices.update(list => list.filter(d => d.id !== device.id));
            this.toastService.success('Device removed successfully');
          },
          error: () => {
            this.toastService.error('Failed to remove device');
          }
        });
      }
    });
  }

  startPairing(): void {
    this.isStartingPairing.set(true);
    this.deviceService.startPairing().subscribe({
      next: (response) => {
        this.pairingCode.set(response.code);
        this.pairingConfirmationPin.set(response.confirmationPin);
        this.pairingStatus.set('pending');
        this.pairingExpiresAt = new Date(response.expiresAt);
        this.isPairingActive.set(true);
        this.isStartingPairing.set(false);
        this.startPairingTimer();
        this.startPairingStatusPolling();
      },
      error: () => {
        this.isStartingPairing.set(false);
        this.toastService.error('Failed to start pairing');
      }
    });
  }

  cancelPairing(): void {
    this.isPairingActive.set(false);
    this.pairingCode.set('');
    this.pairingConfirmationPin.set('');
    this.pairingDeviceIdentifier.set('');
    this.pairingStatus.set('pending');
    this.pairingCodeCopied.set(false);
    this.stopPairingTimer();
    this.stopPairingStatusPolling();
  }

  async copyPairingCode(): Promise<void> {
    const code = this.pairingCode();
    if (!code) return;

    const success = await this.clipboardService.copy(code);
    if (success) {
      this.pairingCodeCopied.set(true);
      setTimeout(() => this.pairingCodeCopied.set(false), 2000);
    }
  }

  refreshFirmware(): void {
    this.firmwareService.refreshFirmwareCheck();
  }

  renderMarkdown(text: string): string {
    return marked(text) as string;
  }

  isVersionLower(deviceVersion: string, latestVersion: string): boolean {
    const parse = (v: string) => v.split('.').map(n => parseInt(n, 10) || 0);
    const [dMaj, dMin, dPat] = parse(deviceVersion);
    const [lMaj, lMin, lPat] = parse(latestVersion);
    if (dMaj !== lMaj) return dMaj < lMaj;
    if (dMin !== lMin) return dMin < lMin;
    return dPat < lPat;
  }

  private startPairingTimer(): void {
    this.updatePairingTimeRemaining();
    this.pairingTimer = setInterval(() => {
      this.updatePairingTimeRemaining();
      if (this.pairingTimeRemaining() <= 0) {
        this.cancelPairing();
        this.loadDevices(); // Reload to pick up newly paired device
        this.toastService.info('Pairing code expired. If the device paired successfully, it will appear in the list.');
      }
    }, 1000);
  }

  private stopPairingTimer(): void {
    if (this.pairingTimer) {
      clearInterval(this.pairingTimer);
      this.pairingTimer = null;
    }
  }

  private startPairingStatusPolling(): void {
    this.pairingStatusTimer = setInterval(() => {
      const code = this.pairingCode();
      if (!code) return;
      this.deviceService.getPairingStatus(code).subscribe({
        next: (response) => {
          if (response.status === 'awaiting_confirmation' && this.pairingStatus() === 'pending') {
            this.pairingDeviceIdentifier.set(response.deviceIdentifier || '');
            this.pairingStatus.set('awaiting_confirmation');
          } else if (response.status === 'confirmed' || response.status === 'completed') {
            this.stopPairingStatusPolling();
            this.cancelPairing();
            this.loadDevices();
            this.toastService.success('Device paired successfully!');
          }
        }
      });
    }, 2000);
  }

  private stopPairingStatusPolling(): void {
    if (this.pairingStatusTimer) {
      clearInterval(this.pairingStatusTimer);
      this.pairingStatusTimer = null;
    }
  }

  confirmPairing(): void {
    const code = this.pairingCode();
    if (!code) return;
    this.pairingStatus.set('confirming');
    this.deviceService.confirmPairing(code).subscribe({
      next: () => {
        this.pairingStatus.set('confirmed');
        this.stopPairingStatusPolling();
        this.cancelPairing();
        this.loadDevices();
        this.toastService.success('Device paired successfully!');
      },
      error: () => {
        this.pairingStatus.set('awaiting_confirmation');
        this.toastService.error('Failed to confirm pairing');
      }
    });
  }

  private updatePairingTimeRemaining(): void {
    if (!this.pairingExpiresAt) {
      this.pairingTimeRemaining.set(0);
      return;
    }
    const now = new Date();
    const remaining = Math.max(0, Math.floor((this.pairingExpiresAt.getTime() - now.getTime()) / 1000));
    this.pairingTimeRemaining.set(remaining);
  }

  ngOnDestroy(): void {
    this.stopPairingTimer();
    this.stopPairingStatusPolling();
  }
}
