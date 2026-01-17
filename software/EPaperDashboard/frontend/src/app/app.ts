import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { RouterOutlet, RouterModule, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './services/auth.service';
import { VersionService } from './services/version.service';
import { ToastContainerComponent } from './components/toast-container/toast-container.component';
import { ConfirmDialogComponent } from './components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterModule, ToastContainerComponent, ConfirmDialogComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  protected readonly authService = inject(AuthService);
  protected readonly versionService = inject(VersionService);
  private readonly router = inject(Router);
  
  // Signal-based theme state
  private readonly theme = signal<string>(this.getInitialTheme());
  readonly themeIcon = computed(() => 
    this.theme() === 'dark' ? 'fa-regular fa-sun' : 'fa-regular fa-moon'
  );

  constructor() {
    // Effect to sync theme changes to DOM
    effect(() => {
      const currentTheme = this.theme();
      document.documentElement.setAttribute('data-bs-theme', currentTheme);
      localStorage.setItem('epaper-theme', currentTheme);
    });
  }

  ngOnInit(): void {
    // Check authentication status on app start
    this.authService.checkAuth();
  }

  logout(): void {
    this.authService.logout().subscribe({
      next: () => {
        this.router.navigate(['/login']);
      }
    });
  }

  toggleTheme(): void {
    const newTheme = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(newTheme);
  }

  private getInitialTheme(): string {
    const stored = localStorage.getItem('epaper-theme');
    return stored || (document.documentElement.getAttribute('data-bs-theme') || 'light');
  }
}
