import { Component, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="app-page py-4 py-sm-5">
      <div class="app-form-shell">
        <div class="text-center mb-4">
          <h1 class="app-page-title">Create your account</h1>
          <p class="app-page-description mx-auto">Set up izBoard to start creating e-paper dashboards.</p>
        </div>
        <form (ngSubmit)="onSubmit()" class="app-form-card" novalidate>
        <div class="mb-3">
          <label class="form-label fw-semibold" for="registerUsername">Username</label>
          <input id="registerUsername" type="text" class="form-control" [(ngModel)]="username" name="username"
            autocomplete="username" required autofocus>
        </div>
        <div class="mb-3">
          <label class="form-label fw-semibold" for="registerPassword">Password</label>
          <div class="input-group">
            <input id="registerPassword" [type]="showPassword ? 'text' : 'password'" class="form-control"
              [(ngModel)]="password" name="password" autocomplete="new-password" required>
            <button type="button" class="btn btn-outline-secondary app-icon-button"
              (click)="showPassword = !showPassword"
              [attr.aria-label]="showPassword ? 'Hide passwords' : 'Show passwords'"
              [attr.aria-pressed]="showPassword">
              <i class="fa-solid" [ngClass]="showPassword ? 'fa-eye-slash' : 'fa-eye'" aria-hidden="true"></i>
            </button>
          </div>
        </div>
        <div class="mb-3">
          <label class="form-label fw-semibold" for="registerPasswordConfirm">Confirm password</label>
          <input id="registerPasswordConfirm" [type]="showPassword ? 'text' : 'password'" class="form-control"
            [(ngModel)]="confirmPassword" name="confirmPassword" autocomplete="new-password" required>
        </div>
        @if (errorMessage()) {
          <div class="alert alert-danger" role="alert">{{ errorMessage() }}</div>
        }
        <button type="submit" class="btn btn-success w-100" [disabled]="isLoading()">
          @if (isLoading()) {
            <span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>
          }
          {{ isLoading() ? 'Registering...' : 'Register' }}
        </button>
        <div class="mt-3 text-center">
          <span class="text-muted">Already have an account?</span> <a routerLink="/login">Sign in</a>
        </div>
        </form>
      </div>
    </div>
  `
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  username = '';
  password = '';
  confirmPassword = '';
  showPassword = false;
  readonly errorMessage = signal('');
  readonly isLoading = signal(false);

  constructor() {
    // Effect to redirect when authenticated
    effect(() => {
      if (this.authService.isAuthReady() && this.authService.isAuthenticated()) {
        this.router.navigate(['/dashboards']);
      }
    });
  }

  onSubmit(): void {
    if (!this.username || !this.password) {
      this.errorMessage.set('Please fill in all fields.');
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');

    this.authService.register({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.router.navigate(['/dashboards']);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Registration failed. Please try again.');
        this.isLoading.set(false);
      }
    });
  }
}
