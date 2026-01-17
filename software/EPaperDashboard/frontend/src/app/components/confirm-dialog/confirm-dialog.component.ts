import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DialogService } from '../../services/dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (dialogService.isOpen()) {
      <div class="modal-backdrop position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center" 
           style="background-color: rgba(0,0,0,0.5); z-index: 2000;"
           (click)="onBackdropClick($event)">
        <div class="card" style="max-width: 400px; min-width: 350px;" (click)="$event.stopPropagation()">
          <div class="card-body">
            <h5 class="card-title mb-3">{{ dialogService.title() }}</h5>
            <p class="card-text mb-4">{{ dialogService.message() }}</p>
            <div class="d-flex gap-2 justify-content-end">
              <button 
                type="button" 
                class="btn btn-secondary"
                [disabled]="dialogService.isLoading()"
                (click)="cancel()"
              >
                {{ dialogService.cancelLabel() }}
              </button>
              <button 
                type="button" 
                [ngClass]="'btn btn-' + (dialogService.isDangerous() ? 'danger' : 'primary')"
                [disabled]="dialogService.isLoading()"
                (click)="confirm()"
              >
                @if (dialogService.isLoading()) {
                  <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                }
                {{ dialogService.confirmLabel() }}
              </button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-backdrop {
      backdrop-filter: blur(2px);
    }
  `]
})
export class ConfirmDialogComponent {
  protected dialogService = inject(DialogService);

  async confirm(): Promise<void> {
    await this.dialogService.handleConfirm();
  }

  cancel(): void {
    this.dialogService.handleCancel();
  }

  onBackdropClick(event: MouseEvent): void {
    // Prevent closing when clicking outside (modal behavior)
    event.stopPropagation();
  }
}

