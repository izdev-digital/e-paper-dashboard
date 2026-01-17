import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'warning' | 'info';
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private toastIdCounter = 0;

  show(message: string, type: 'success' | 'error' | 'warning' | 'info' = 'info', duration = 5000): string {
    const id = `toast-${this.toastIdCounter++}`;
    const toast: Toast = { id, message, type, duration };
    
    // Remove any existing toasts and add new one
    this.toasts.set([toast]);
    
    // Auto-remove after duration
    if (duration > 0) {
      setTimeout(() => {
        this.remove(id);
      }, duration);
    }
    
    return id;
  }

  remove(id: string): void {
    const current = this.toasts();
    this.toasts.set(current.filter(t => t.id !== id));
  }

  clear(): void {
    this.toasts.set([]);
  }

  success(message: string, duration = 5000): string {
    return this.show(message, 'success', duration);
  }

  error(message: string, duration = 5000): string {
    return this.show(message, 'error', duration);
  }

  warning(message: string, duration = 5000): string {
    return this.show(message, 'warning', duration);
  }

  info(message: string, duration = 5000): string {
    return this.show(message, 'info', duration);
  }
}
