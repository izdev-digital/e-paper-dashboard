import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'warning' | 'info';
  duration?: number;
  actionLabel?: string;
  action?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private toastIdCounter = 0;
  private readonly timers = new Map<string, ReturnType<typeof setTimeout>>();
  private readonly deadlines = new Map<string, number>();
  private readonly remainingDurations = new Map<string, number>();

  show(message: string, type: 'success' | 'error' | 'warning' | 'info' = 'info', duration = 5000, actionLabel?: string, action?: () => void): string {
    const id = `toast-${this.toastIdCounter++}`;
    const toast: Toast = { id, message, type, duration, actionLabel, action };

    this.toasts.set([toast]);

    if (duration > 0) {
      this.scheduleRemoval(id, duration);
    }

    return id;
  }

  showWithAction(message: string, actionLabel: string, action: () => void, type: 'success' | 'error' | 'warning' | 'info' = 'info', duration = 5000): string {
    return this.show(message, type, duration, actionLabel, action);
  }

  remove(id: string): void {
    const timer = this.timers.get(id);
    if (timer) clearTimeout(timer);
    this.timers.delete(id);
    this.deadlines.delete(id);
    this.remainingDurations.delete(id);
    const current = this.toasts();
    this.toasts.set(current.filter(t => t.id !== id));
  }

  clear(): void {
    for (const timer of this.timers.values()) clearTimeout(timer);
    this.timers.clear();
    this.deadlines.clear();
    this.remainingDurations.clear();
    this.toasts.set([]);
  }

  pause(id: string): void {
    const timer = this.timers.get(id);
    const deadline = this.deadlines.get(id);
    if (!timer || deadline == null) return;
    clearTimeout(timer);
    this.timers.delete(id);
    this.remainingDurations.set(id, Math.max(deadline - Date.now(), 250));
  }

  resume(id: string): void {
    const remaining = this.remainingDurations.get(id);
    if (remaining == null || !this.toasts().some(toast => toast.id === id)) return;
    this.remainingDurations.delete(id);
    this.scheduleRemoval(id, remaining);
  }

  private scheduleRemoval(id: string, duration: number): void {
    this.deadlines.set(id, Date.now() + duration);
    this.timers.set(id, setTimeout(() => this.remove(id), duration));
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
