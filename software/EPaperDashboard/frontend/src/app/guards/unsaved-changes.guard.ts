import { CanDeactivateFn } from '@angular/router';
import { inject } from '@angular/core';
import { DialogService } from '../services/dialog.service';

export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }

  const dialogService = inject(DialogService);

  return new Promise<boolean>((resolve) => {
    dialogService.confirm({
      title: 'Unsaved Changes',
      message: 'You have unsaved changes. Are you sure you want to leave without saving?',
      confirmLabel: 'Leave',
      isDangerous: true,
      onConfirm: () => resolve(true),
      onCancel: () => resolve(false),
    });
  });
};
