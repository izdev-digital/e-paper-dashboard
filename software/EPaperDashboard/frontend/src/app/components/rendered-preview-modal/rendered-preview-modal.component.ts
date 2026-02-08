import { Component, Input, Output, EventEmitter, signal, Signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-rendered-preview-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isOpen()) {
      <div class="position-fixed top-0 start-0 w-100 h-100" style="background-color: rgba(0,0,0,0.5); z-index: 1050; overflow: hidden;">
        <div class="position-absolute top-50 start-50 translate-middle rounded" style="width: 90vw; height: 90vh; max-width: 900px; max-height: 600px; display: flex; flex-direction: column; background-color: var(--bs-body-bg); color: var(--bs-body-color); border: 1px solid var(--bs-border-color);">
          <!-- Modal Header -->
          <div class="d-flex justify-content-between align-items-center p-3" style="border-bottom: 1px solid var(--bs-border-color);">
            <h5 class="mb-0">{{ title }}</h5>
            <div class="d-flex gap-2 align-items-center">
              <button type="button" class="btn btn-sm" title="Reload Preview" (click)="onReloadClick()">
                <i class="fa-solid fa-arrows-rotate"></i>
              </button>
              <button type="button" class="btn-close" aria-label="Close" (click)="onCloseClick()"></button>
            </div>
          </div>
          <!-- Modal Body -->
          <div class="flex-grow-1 overflow-auto p-3 d-flex justify-content-center align-items-flex-start" style="background-color: var(--bs-secondary-bg);">
            @if (isLoading()) {
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            } @else if (error()) {
              <div class="alert alert-danger mb-0">{{ error() }}</div>
            } @else if (imageUrl()) {
              <img [src]="imageUrl()" style="max-width: 100%; height: auto; object-fit: contain;" [alt]="title" />
            }
          </div>
        </div>
      </div>
    }
  `
})
export class RenderedPreviewModalComponent {
  @Input() title = 'Preview';
  @Input() isOpen!: Signal<boolean>;
  @Input() isLoading!: Signal<boolean>;
  @Input() error!: Signal<string>;
  @Input() imageUrl!: Signal<string>;
  @Output() close = new EventEmitter<void>();
  @Output() reload = new EventEmitter<void>();

  onCloseClick(): void {
    this.close.emit();
  }

  onReloadClick(): void {
    this.reload.emit();
  }
}
