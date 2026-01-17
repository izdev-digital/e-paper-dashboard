import { Injectable, signal } from '@angular/core';

export interface DialogConfig {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  isDangerous?: boolean;
  onConfirm?: () => void | Promise<void>;
  onCancel?: () => void;
}

export interface DialogState {
  isOpen: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  isDangerous: boolean;
  isLoading: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class DialogService {
  private readonly dialogState = signal<DialogState>({
    isOpen: false,
    title: '',
    message: '',
    confirmLabel: 'Confirm',
    cancelLabel: 'Cancel',
    isDangerous: false,
    isLoading: false
  });

  // Expose as readable signals
  readonly isOpen = () => this.dialogState().isOpen;
  readonly title = () => this.dialogState().title;
  readonly message = () => this.dialogState().message;
  readonly confirmLabel = () => this.dialogState().confirmLabel;
  readonly cancelLabel = () => this.dialogState().cancelLabel;
  readonly isDangerous = () => this.dialogState().isDangerous;
  readonly isLoading = () => this.dialogState().isLoading;

  private onConfirm: (() => void | Promise<void>) | null = null;
  private onCancel: (() => void) | null = null;

  /**
   * Show a confirmation dialog
   * @example
   * this.dialogService.confirm({
   *   title: 'Delete Item?',
   *   message: 'This action cannot be undone.',
   *   isDangerous: true,
   *   onConfirm: async () => {
   *     await this.deleteService.delete(id);
   *     this.toastService.success('Deleted successfully');
   *   }
   * });
   */
  async confirm(config: DialogConfig): Promise<void> {
    this.onConfirm = config.onConfirm || null;
    this.onCancel = config.onCancel || null;

    this.dialogState.set({
      isOpen: true,
      title: config.title,
      message: config.message,
      confirmLabel: config.confirmLabel || 'Confirm',
      cancelLabel: config.cancelLabel || 'Cancel',
      isDangerous: config.isDangerous || false,
      isLoading: false
    });
  }

  /**
   * Handle confirm action (called from dialog component)
   */
  async handleConfirm(): Promise<void> {
    if (!this.onConfirm) {
      this.close();
      return;
    }

    this.setLoading(true);
    try {
      await Promise.resolve(this.onConfirm());
    } finally {
      this.setLoading(false);
      this.close();
    }
  }

  /**
   * Handle cancel action (called from dialog component)
   */
  handleCancel(): void {
    this.onCancel?.();
    this.close();
  }

  /**
   * Update loading state
   */
  private setLoading(loading: boolean): void {
    const current = this.dialogState();
    this.dialogState.set({
      ...current,
      isLoading: loading
    });
  }

  /**
   * Close the dialog
   */
  close(): void {
    this.dialogState.set({
      isOpen: false,
      title: '',
      message: '',
      confirmLabel: 'Confirm',
      cancelLabel: 'Cancel',
      isDangerous: false,
      isLoading: false
    });
    this.onConfirm = null;
    this.onCancel = null;
  }
}
