import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, Toast } from '../../services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container position-fixed bottom-0 end-0 p-3" style="z-index: 9999;">
      @for (toast of toastService.toasts(); track toast.id) {
        <div 
          class="toast show" 
          [ngClass]="'toast-' + toast.type"
          role="alert"
        >
          <div class="d-flex align-items-center gap-2">
            @if (toast.type === 'success') {
              <i class="fa-solid fa-check-circle text-success"></i>
            }
            @if (toast.type === 'error') {
              <i class="fa-solid fa-exclamation-circle text-danger"></i>
            }
            @if (toast.type === 'warning') {
              <i class="fa-solid fa-exclamation-triangle text-warning"></i>
            }
            @if (toast.type === 'info') {
              <i class="fa-solid fa-info-circle text-info"></i>
            }
            <span>{{ toast.message }}</span>
            <button 
              type="button" 
              class="btn-close ms-auto" 
              (click)="toastService.remove(toast.id)"
            ></button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast {
      min-width: 300px;
      max-width: 500px;
      box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15);
      border-radius: 0.375rem;
      padding: 0.75rem 1rem;
      margin-bottom: 0.75rem;
    }
    .toast-success {
      background-color: #d1e7dd;
      color: #0a3622;
      border: 1px solid #badbcc;
    }
    .toast-error {
      background-color: #f8d7da;
      color: #842029;
      border: 1px solid #f5c2c7;
    }
    .toast-warning {
      background-color: #fff3cd;
      color: #664d03;
      border: 1px solid #ffecb5;
    }
    .toast-info {
      background-color: #cfe2ff;
      color: #084298;
      border: 1px solid #b6d4fe;
    }
  `]
})
export class ToastContainerComponent {
  protected toastService = inject(ToastService);
}
