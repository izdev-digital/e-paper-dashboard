import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // With signals, we can synchronously check if auth is ready
  if (!authService.isAuthReady()) {
    // Auth not ready yet - wait briefly
    return new Promise<boolean>((resolve) => {
      const checkInterval = setInterval(() => {
        if (authService.isAuthReady()) {
          clearInterval(checkInterval);
          const canActivate = authService.isAuthenticated();
          if (!canActivate) {
            router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
          }
          resolve(canActivate);
        }
      }, 10);
      
      // Timeout after 5 seconds
      setTimeout(() => {
        clearInterval(checkInterval);
        router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
        resolve(false);
      }, 5000);
    });
  }

  const canActivate = authService.isAuthenticated();
  if (!canActivate) {
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  }
  return canActivate;
};
