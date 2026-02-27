import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DeviceService, Device } from '../../services/device.service';
import { DashboardService } from '../../services/dashboard.service';
import { FirmwareService } from '../../services/firmware.service';
import { AuthService } from '../../services/auth.service';
import { DialogService } from '../../services/dialog.service';
import { ToastService } from '../../services/toast.service';
import { ToastContainerComponent } from '../toast-container/toast-container.component';
import { Dashboard } from '../../models/types';

@Component({
  selector: 'app-device-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, ToastContainerComponent],
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
          <div class="text-muted small">
            <div><i class="fa-solid fa-clock"></i> Expires in {{ pairingTimeRemaining() }} seconds</div>
            <div><i class="fa-solid fa-info-circle"></i> Enter this code on your device web interface</div>
          </div>
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
            </div>
            <div class="mt-2 d-flex align-items-center gap-2">
              <i class="fa-solid fa-display text-muted"></i>
              <label class="small text-muted mb-0">Dashboard:</label>
              <select class="form-select form-select-sm dashboard-select"
                [ngModel]="device.dashboardId || ''"
                (ngModelChange)="assignDashboard(device, $event)">
                <option value="">— No dashboard assigned —</option>
                @for (dashboard of dashboards(); track dashboard.id) {
                  <option [value]="dashboard.id">{{ dashboard.name }}</option>
                }
              </select>
            </div>
          </div>
        }
      </div>
    } @else {
      <div class="alert alert-info">
        <i class="fa-solid fa-info-circle"></i> No devices paired yet. Click "Pair New Device" to get started.
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

  readonly devices = signal<Device[]>([]);
  readonly dashboards = signal<Dashboard[]>([]);
  readonly isLoading = signal(false);

  readonly isPairingActive = signal(false);
  readonly pairingCode = signal('');
  readonly pairingTimeRemaining = signal(0);
  readonly isStartingPairing = signal(false);
  readonly pairingCodeCopied = signal(false);

  readonly firmwareInfo = this.firmwareService.firmwareInfo;

  private pairingTimer: any = null;
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
      dashboardId: dashboardId || null
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
        this.pairingExpiresAt = new Date(response.expiresAt);
        this.isPairingActive.set(true);
        this.isStartingPairing.set(false);
        this.startPairingTimer();
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
    this.pairingCodeCopied.set(false);
    this.stopPairingTimer();
  }

  copyPairingCode(): void {
    const code = this.pairingCode();
    if (!code) return;
    navigator.clipboard.writeText(code).then(() => {
      this.pairingCodeCopied.set(true);
      setTimeout(() => this.pairingCodeCopied.set(false), 2000);
    });
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
  }
}
