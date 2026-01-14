import { Component, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mt-5">
      <h2 class="text-center">Register</h2>
      <form (ngSubmit)="onSubmit()" class="w-50 mx-auto">
        <div class="mb-3">
          <label class="form-label">Username</label>
          <input type="text" class="form-control" [(ngModel)]="username" name="username" required>
        </div>
        <div class="mb-3">
          <label class="form-label">Password</label>
          <input type="password" class="form-control" [(ngModel)]="password" name="password" required>
        </div>
        @if (errorMessage()) {
          <div class="alert alert-danger">{{ errorMessage() }}</div>
        }
        <button type="submit" class="btn btn-success w-100" [disabled]="isLoading()">
          {{ isLoading() ? 'Registering...' : 'Register' }}
        </button>
        <div class="mt-3 text-center">
          <a routerLink="/login">Already have an account? Login</a>
        </div>
      </form>
    </div>
  `
})
export class RegisterComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  username = '';
  password = '';
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
