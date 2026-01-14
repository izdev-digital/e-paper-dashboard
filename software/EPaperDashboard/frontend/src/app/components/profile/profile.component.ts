import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { HttpClient } from '@angular/common/http';

interface ChangeNicknameRequest {
  newNickname: string;
}

interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class ProfileComponent {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly newNickname = signal('');
  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmNewPassword = signal('');
  readonly successMessage = signal('');
  readonly errorMessage = signal('');
  readonly isChangingNickname = signal(false);
  readonly isChangingPassword = signal(false);
  readonly isDeletingProfile = signal(false);

  get currentUser() {
    return this.authService.currentUser();
  }

  changeNickname(): void {
    this.clearMessages();
    this.isChangingNickname.set(true);

    const request: ChangeNicknameRequest = {
      newNickname: this.newNickname()
    };

    this.http.post('/api/users/change-nickname', request).subscribe({
      next: () => {
        const message = this.newNickname().trim() 
          ? 'Nickname changed successfully.' 
          : 'Nickname cleared.';
        this.successMessage.set(message);
        this.newNickname.set('');
        this.isChangingNickname.set(false);
        // Refresh user data
        this.authService.getCurrentUser().subscribe();
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Nickname change failed.');
        this.isChangingNickname.set(false);
      }
    });
  }

  changePassword(): void {
    this.clearMessages();

    if (!this.currentPassword() || !this.newPassword() || !this.confirmNewPassword()) {
      this.errorMessage.set('All password fields are required.');
      return;
    }

    if (this.newPassword() !== this.confirmNewPassword()) {
      this.errorMessage.set('New password and confirmation do not match.');
      return;
    }

    this.isChangingPassword.set(true);

    const request: ChangePasswordRequest = {
      currentPassword: this.currentPassword(),
      newPassword: this.newPassword(),
      confirmNewPassword: this.confirmNewPassword()
    };

    this.http.post('/api/users/change-password', request).subscribe({
      next: () => {
        this.successMessage.set('Password changed successfully. Please log in again.');
        this.clearPasswordFields();
        this.isChangingPassword.set(false);
        // Log out after password change
        setTimeout(() => {
          this.authService.logout().subscribe(() => {
            this.router.navigate(['/login']);
          });
        }, 2000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Password change failed.');
        this.isChangingPassword.set(false);
      }
    });
  }

  deleteProfile(): void {
    if (!confirm('Are you sure you want to delete your profile? This action cannot be undone.')) {
      return;
    }

    this.isDeletingProfile.set(true);

    this.http.delete('/api/users/delete-profile').subscribe({
      next: () => {
        this.authService.logout().subscribe(() => {
          this.router.navigate(['/home']);
        });
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to delete profile.');
        this.isDeletingProfile.set(false);
      }
    });
  }

  private clearMessages(): void {
    this.successMessage.set('');
    this.errorMessage.set('');
  }

  private clearPasswordFields(): void {
    this.currentPassword.set('');
    this.newPassword.set('');
    this.confirmNewPassword.set('');
  }
}
