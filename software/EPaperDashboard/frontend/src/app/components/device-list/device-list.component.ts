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

    .pairing-steps {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.75rem;
      counter-reset: pairing-step;
    }

    .pairing-step {
      position: relative;
      padding-left: 2.25rem;
      min-height: 2rem;
      color: var(--bs-secondary-color);
      font-size: 0.875rem;
    }

    .pairing-step::before {
      counter-increment: pairing-step;
      content: counter(pairing-step);
      position: absolute;
      left: 0;
      display: grid;
      place-items: center;
      width: 1.65rem;
      height: 1.65rem;
      border-radius: 50%;
      color: var(--bs-primary-text-emphasis);
      background: var(--bs-primary-bg-subtle);
      font-weight: 700;
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

      .pairing-steps {
        grid-template-columns: 1fr;
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
        <button type="button" class="btn btn-primary" (click)="startPairing()">
          <i class="fa-solid fa-plus" aria-hidden="true"></i>
          <span class="d-none d-sm-inline">Pair device</span>
          <span class="visually-hidden d-sm-none">Pair device</span>
        </button>
      }
    </div>

    @if (isPairingActive()) {
      <div class="alert alert-info mb-4">
        <div class="d-flex flex-column gap-2">
          <div class="d-flex justify-content-between align-items-start">
            <div>
              <div class="mb-2">
                <strong>Pair a new device</strong>
              </div>
              <p class="text-muted mb-0">
                {{ pairingMode() === 'device'
                  ? 'The display creates the claim code. Keep this page open while you configure it.'
                  : 'Legacy pairing for firmware older than 0.4.0.' }}
              </p>
            </div>
            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="cancelPairing()">
              <i class="fa-solid fa-times"></i> Cancel
            </button>
          </div>

          @if (pairingMode() === 'device') {
            <div class="pairing-steps mt-3">
              <div class="pairing-step">
                Copy the server URL before leaving this network:
                @if (serverUrl()) {
                  <code>{{ serverUrl() }}</code>
                } @else {
                  <span class="text-danger">CLIENT_URL is not configured</span>
                }
                <button type="button" class="btn btn-sm btn-link p-1" (click)="copyServerUrl()" aria-label="Copy server URL">
                  <i class="fa-solid" [ngClass]="serverUrlCopied() ? 'fa-check' : 'fa-copy'" aria-hidden="true"></i>
                </button>
              </div>
              <div class="pairing-step">Scan the Wi-Fi QR on the display, open its setup page, then enter your home Wi-Fi and paste the server URL. Submit once.</div>
              <div class="pairing-step">
                After submitting, reconnect to your normal network. Enter the code shown on the display, then claim it.
                <div class="input-group mt-2">
                  <input type="text" class="form-control font-monospace text-uppercase"
                    aria-label="Device claim code" placeholder="ABC123" maxlength="6"
                    autocomplete="one-time-code" [value]="pairingCode()" (input)="updateClaimCode($event)"
                    [disabled]="isAwaitingDevice()">
                  <button type="button" class="btn btn-primary" (click)="claimDevice()"
                    [disabled]="pairingCode().length !== 6 || isClaiming() || isAwaitingDevice() || !serverUrl()">
                    {{ isClaiming() ? 'Claiming…' : isAwaitingDevice() ? 'Waiting for display…' : 'Claim device' }}
                  </button>
                </div>
                @if (isAwaitingDevice()) {
                  <div class="small mt-2" role="status">
                    <i class="fa-solid fa-spinner fa-spin me-1" aria-hidden="true"></i>
                    Claim accepted. Waiting for the display to confirm receipt ({{ formattedPairingTime() }}).
                  </div>
                }
              </div>
            </div>
            <button type="button" class="btn btn-sm btn-link align-self-start px-0" (click)="startLegacyPairing()">
              Pair a display running firmware older than 0.4.0
            </button>
          } @else {
            <div class="alert alert-warning mb-2" role="status">
              Enter code <strong class="font-monospace fs-5">{{ pairingCode() }}</strong> in the display setup portal.
              <button type="button" class="btn btn-sm btn-link" (click)="copyPairingCode()" aria-label="Copy legacy pairing code">
                <i class="fa-solid" [ngClass]="pairingCodeCopied() ? 'fa-check' : 'fa-copy'" aria-hidden="true"></i>
              </button>
              The code expires in {{ formattedPairingTime() }}.
            </div>
            <div class="pairing-steps">
              <div class="pairing-step">Copy server URL <code>{{ serverUrl() }}</code> and pairing code <strong>{{ pairingCode() }}</strong>.</div>
              <div class="pairing-step">Connect to the display setup Wi-Fi and enter home Wi-Fi, the server URL, and pairing code.</div>
              <div class="pairing-step"><i class="fa-solid fa-spinner fa-spin me-1" aria-hidden="true"></i>Waiting for the display to register.</div>
            </div>
            <button type="button" class="btn btn-sm btn-link align-self-start px-0" (click)="useCurrentPairing()">
              Back to current pairing
            </button>
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
  readonly serverUrl = signal('');

  readonly isPairingActive = signal(false);
  readonly pairingCode = signal('');
  readonly pairingMode = signal<'device' | 'legacy'>('device');
  readonly isClaiming = signal(false);
  readonly isAwaitingDevice = signal(false);
  readonly pairingCodeCopied = signal(false);
  readonly pairingTimeRemaining = signal(0);
  readonly serverUrlCopied = signal(false);

  private pairingTimer: ReturnType<typeof setInterval> | null = null;
  private pairingStatusTimer: ReturnType<typeof setInterval> | null = null;
  private pairingExpiresAt: Date | null = null;

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
    this.loadPairingConfiguration();
  }

  private loadPairingConfiguration(): void {
    this.deviceService.getPairingConfiguration().subscribe({
      next: (configuration) => this.serverUrl.set(configuration.clientUrl),
      error: () => this.serverUrl.set('')
    });
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
      error: (err) => {
        this.toastService.error(err.error?.message || 'Failed to update device');
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
    this.stopLegacyPairingTimers();
    this.pairingMode.set('device');
    this.pairingCode.set('');
    this.isAwaitingDevice.set(false);
    this.isPairingActive.set(true);
    if (!this.serverUrl()) this.loadPairingConfiguration();
  }

  cancelPairing(): void {
    this.stopLegacyPairingTimers();
    this.isPairingActive.set(false);
    this.pairingCode.set('');
    this.isAwaitingDevice.set(false);
    this.pairingMode.set('device');
    this.pairingCodeCopied.set(false);
    this.serverUrlCopied.set(false);
  }

  startLegacyPairing(): void {
    this.deviceService.startPairing().subscribe({
      next: (response) => {
        this.stopLegacyPairingTimers();
        this.pairingMode.set('legacy');
        this.pairingCode.set(response.code);
        this.pairingExpiresAt = new Date(response.expiresAt);
        this.startLegacyPairingTimers();
      },
      error: () => this.toastService.error('Failed to start legacy pairing')
    });
  }

  useCurrentPairing(): void {
    this.stopLegacyPairingTimers();
    this.pairingMode.set('device');
    this.pairingCode.set('');
  }

  formattedPairingTime(): string {
    const seconds = this.pairingTimeRemaining();
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
  }

  async copyPairingCode(): Promise<void> {
    if (!this.pairingCode()) return;
    if (await this.clipboardService.copy(this.pairingCode())) {
      this.pairingCodeCopied.set(true);
      setTimeout(() => this.pairingCodeCopied.set(false), 2000);
    }
  }

  updateClaimCode(event: Event): void {
    const input = event.target as HTMLInputElement;
    const code = input.value.toUpperCase().replace(/[^0-9A-Z]/g, '').slice(0, 6);
    input.value = code;
    this.pairingCode.set(code);
  }

  claimDevice(): void {
    const code = this.pairingCode();
    if (code.length !== 6 || this.isClaiming()) return;

    this.isClaiming.set(true);
    this.deviceService.claimDevice(code).subscribe({
      next: (response) => {
        this.isClaiming.set(false);
        this.isAwaitingDevice.set(true);
        this.startDeviceAcknowledgementPolling(code, response.acknowledgementExpiresAt);
      },
      error: (err) => {
        this.isClaiming.set(false);
        this.toastService.error(err.error?.detail || 'The claim code is invalid, expired, or not announced yet.');
      }
    });
  }

  async copyServerUrl(): Promise<void> {
    const success = await this.clipboardService.copy(this.serverUrl());
    if (success) {
      this.serverUrlCopied.set(true);
      setTimeout(() => this.serverUrlCopied.set(false), 2000);
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

  private startLegacyPairingTimers(): void {
    this.updateLegacyPairingTime();
    this.pairingTimer = setInterval(() => {
      this.updateLegacyPairingTime();
      if (this.pairingTimeRemaining() <= 0) {
        this.cancelPairing();
        this.toastService.info('Legacy pairing code expired.');
      }
    }, 1000);
    this.pairingStatusTimer = setInterval(() => {
      if (!this.pairingCode()) return;
      this.deviceService.getPairingStatus(this.pairingCode()).subscribe({
        next: (response) => {
          if (response.status === 'completed') {
            this.cancelPairing();
            this.loadDevices();
            this.toastService.success('Device paired successfully.');
          }
        }
      });
    }, 2000);
  }

  private startDeviceAcknowledgementPolling(code: string, acknowledgementExpiresAt: string): void {
    this.stopLegacyPairingTimers();
    this.pairingExpiresAt = new Date(acknowledgementExpiresAt);
    this.updateLegacyPairingTime();

    const poll = () => {
      this.deviceService.getPairingStatus(code).subscribe({
        next: (response) => {
          if (!this.isAwaitingDevice()) return;
          this.pairingExpiresAt = new Date(response.expiresAt);
          if (response.status === 'completed') {
            this.cancelPairing();
            this.loadDevices();
            this.toastService.success('Device paired successfully.');
          }
        },
        error: (err) => {
          if (!this.isAwaitingDevice()) return;
          if (err.status === 404 || err.status === 410) {
            this.cancelPairing();
            this.toastService.error('The display did not confirm pairing in time. Start pairing again.');
          }
        }
      });
    };

    poll();
    this.pairingStatusTimer = setInterval(poll, 2000);
    this.pairingTimer = setInterval(() => {
      this.updateLegacyPairingTime();
      if (this.pairingTimeRemaining() <= 0) {
        this.cancelPairing();
        this.toastService.error('The display did not confirm pairing in time. Start pairing again.');
      }
    }, 1000);
  }

  private updateLegacyPairingTime(): void {
    const remaining = this.pairingExpiresAt
      ? Math.max(0, Math.floor((this.pairingExpiresAt.getTime() - Date.now()) / 1000))
      : 0;
    this.pairingTimeRemaining.set(remaining);
  }

  private stopLegacyPairingTimers(): void {
    if (this.pairingTimer) clearInterval(this.pairingTimer);
    if (this.pairingStatusTimer) clearInterval(this.pairingStatusTimer);
    this.pairingTimer = null;
    this.pairingStatusTimer = null;
    this.pairingExpiresAt = null;
  }

  ngOnDestroy(): void {
    this.stopLegacyPairingTimers();
  }

}
