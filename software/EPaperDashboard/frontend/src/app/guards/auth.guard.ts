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
      const checkInterval = setInterval(() => {
        if (authService.isAuthReady()) {
          clearInterval(checkInterval);
          const canActivate = authService.isAuthenticated();
          console.log('Auth now ready, authenticated:', canActivate);
          if (!canActivate) {
            console.log('Redirecting to login from:', state.url);
            router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
          }
          resolve(canActivate);
        }
      }, 10);
      
      // Timeout after 5 seconds
      setTimeout(() => {
        clearInterval(checkInterval);
        console.log('Auth guard timeout, redirecting to login');
        router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
        resolve(false);
      }, 5000);
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
