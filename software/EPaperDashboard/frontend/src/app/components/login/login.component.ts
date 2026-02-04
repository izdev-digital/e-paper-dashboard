import { Component, inject, OnInit, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mt-5">
      <h2 class="text-center">Login</h2>
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
        <button type="submit" class="btn btn-primary w-100" [disabled]="isLoading()">
          {{ isLoading() ? 'Logging in...' : 'Login' }}
        </button>
        <div class="mt-3 text-center">
          <a routerLink="/register">Don't have an account? Register</a>
        </div>
      </form>
    </div>
  `
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  username = '';
  password = '';
  readonly errorMessage = signal('');
  readonly isLoading = signal(false);
  private returnUrl = '/dashboards';
  private hasRedirected = false;

  constructor() {
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboards';
    
    effect(() => {
      if (!this.hasRedirected && 
          this.router.url.startsWith('/login') &&
          this.authService.isAuthReady() && 
          this.authService.isAuthenticated()) {
        this.hasRedirected = true;
        this.router.navigate([this.returnUrl]);
      }
    });
  }

  ngOnInit(): void {
    // Return URL is already set in constructor
  }

  onSubmit(): void {
    if (!this.username || !this.password) {
      this.errorMessage.set('Please fill in all fields.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');

    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.router.navigate([this.returnUrl]);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Invalid username or password.');
        this.isLoading.set(false);
      }
    });
  }
}
