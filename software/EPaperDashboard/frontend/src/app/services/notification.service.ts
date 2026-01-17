import { Injectable, inject } from '@angular/core';
import { ToastService } from './toast.service';
import { DialogService, DialogConfig } from './dialog.service';

/**
 * Unified notification service combining dialogs and toasts
 * Provides a single interface for all user notifications
 */
@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private readonly toastService = inject(ToastService);
  private readonly dialogService = inject(DialogService);

  // ============ Toast Methods ============

  /**
   * Show success toast notification
   * @example
   * this.notification.success('Changes saved successfully');
   */
  success(message: string, duration?: number): string {
    return this.toastService.success(message, duration);
  }

  /**
   * Show error toast notification
   * @example
   * this.notification.error('Failed to save changes');
   */
  error(message: string, duration?: number): string {
    return this.toastService.error(message, duration);
  }

  /**
   * Show warning toast notification
   * @example
   * this.notification.warning('This action is risky');
   */
  warning(message: string, duration?: number): string {
    return this.toastService.warning(message, duration);
  }

  /**
   * Show info toast notification
   * @example
   * this.notification.info('Please check your email');
   */
  info(message: string, duration?: number): string {
    return this.toastService.info(message, duration);
  }

  /**
   * Remove a specific toast
   */
  removeToast(id: string): void {
    this.toastService.remove(id);
  }

  /**
   * Clear all toasts
   */
  clearToasts(): void {
    this.toastService.clear();
  }

  // ============ Dialog Methods ============

  /**
   * Show a confirmation dialog with async support
   * @example
   * await this.notification.confirm({
   *   title: 'Delete Item?',
   *   message: 'This action cannot be undone.',
   *   isDangerous: true,
   *   onConfirm: async () => {
   *     await this.apiService.delete(id);
   *     this.notification.success('Deleted successfully');
   *   },
   *   onCancel: () => console.log('Cancelled')
   * });
   */
  async confirm(config: DialogConfig): Promise<void> {
    return this.dialogService.confirm(config);
  }

  /**
   * Show a simple confirmation dialog (convenience method)
   * @example
   * await this.notification.confirmDelete('User', async () => {
   *   await this.apiService.deleteUser(userId);
   * });
   */
  async confirmDelete(itemName: string, onConfirm: () => void | Promise<void>): Promise<void> {
    return this.dialogService.confirm({
      title: `Delete ${itemName}?`,
      message: `Are you sure you want to delete this ${itemName}? This action cannot be undone.`,
      confirmLabel: 'Delete',
      cancelLabel: 'Cancel',
      isDangerous: true,
      onConfirm
    });
  }

  /**
   * Show a dangerous action confirmation dialog
   * @example
   * await this.notification.confirmDangerous({
   *   title: 'Reset Configuration?',
   *   message: 'All settings will be lost.',
   *   onConfirm: async () => {
   *     await this.configService.reset();
   *   }
   * });
   */
  async confirmDangerous(config: Omit<DialogConfig, 'isDangerous'>): Promise<void> {
    return this.dialogService.confirm({
      ...config,
      isDangerous: true
    });
  }

  /**
   * Close any open dialog
   */
  closeDialog(): void {
    this.dialogService.close();
  }

  // ============ Combined Methods ============

  /**
   * Execute an async operation with loading state and notifications
   * @example
   * await this.notification.execute(async () => {
   *   return await this.apiService.save(data);
   * }, {
   *   successMessage: 'Saved successfully',
   *   errorMessage: 'Failed to save'
   * });
   */
  async execute<T>(
    operation: () => Promise<T>,
    options: {
      successMessage?: string;
      errorMessage?: string;
      loadingMessage?: string;
    } = {}
  ): Promise<T | null> {
    try {
      const result = await operation();
      if (options.successMessage) {
        this.success(options.successMessage);
      }
      return result;
    } catch (error) {
      const errorMessage = options.errorMessage || 'An error occurred';
      this.error(errorMessage);
      console.error('Operation failed:', error);
      return null;
    }
  }

  /**
   * Show a confirmation dialog and execute an operation if confirmed
   * @example
   * await this.notification.confirmAndExecute(
   *   {
   *     title: 'Save Changes?',
   *     message: 'Do you want to save all changes?',
   *     confirmLabel: 'Save'
   *   },
   *   async () => {
   *     return await this.apiService.save(data);
   *   },
   *   {
   *     successMessage: 'Changes saved',
   *     errorMessage: 'Failed to save'
   *   }
   * );
   */
  async confirmAndExecute<T>(
    dialogConfig: Omit<DialogConfig, 'onConfirm' | 'onCancel'>,
    operation: () => Promise<T>,
    options: {
      successMessage?: string;
      errorMessage?: string;
    } = {}
  ): Promise<T | null> {
    return new Promise((resolve) => {
      this.dialogService.confirm({
        ...dialogConfig,
        onConfirm: async () => {
          const result = await this.execute(operation, options);
          resolve(result);
        },
        onCancel: () => {
          resolve(null);
        }
      });
    });
  }
}
