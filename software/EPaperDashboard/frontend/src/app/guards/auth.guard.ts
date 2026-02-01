import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  console.log('=== AUTH GUARD ===');
  console.log('Checking route:', state.url);
  console.log('Auth ready:', authService.isAuthReady());
  console.log('Is authenticated:', authService.isAuthenticated());

  // With signals, we can synchronously check if auth is ready
  if (!authService.isAuthReady()) {
    console.log('Auth not ready, waiting...');
    // Auth not ready yet - wait briefly
    return new Promise<boolean>((resolve) => {
      let resolved = false;
      const maxAttempts = 500; // 5 seconds max
      let attempts = 0;
      
      const checkInterval = setInterval(() => {
        attempts++;
        if (authService.isAuthReady()) {
          clearInterval(checkInterval);
          if (!resolved) {
            resolved = true;
            const canActivate = authService.isAuthenticated();
            console.log('Auth ready after', attempts * 10, 'ms. Authenticated:', canActivate);
            if (!canActivate) {
              console.log('Not authenticated, redirecting to login');
              router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
            }
            resolve(canActivate);
          }
        } else if (attempts >= maxAttempts) {
          clearInterval(checkInterval);
          if (!resolved) {
            resolved = true;
            console.log('Auth guard timeout after 5 seconds, redirecting to login');
            router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
            resolve(false);
          }
        }
      }, 10);
    });
  }

  const canActivate = authService.isAuthenticated();
  console.log('Auth guard result:', canActivate);
  if (!canActivate) {
    console.log('Not authenticated, redirecting to login from:', state.url);
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  }
  return canActivate;
};
