import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthReady()) {
    return new Promise<boolean>((resolve) => {
      let resolved = false;
      const maxAttempts = 500;
      let attempts = 0;

      const checkInterval = setInterval(() => {
        attempts++;
        if (authService.isAuthReady()) {
          clearInterval(checkInterval);
          if (!resolved) {
            resolved = true;
            const canActivate = authService.isAuthenticated();
            if (!canActivate) {
              router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
            }
            resolve(canActivate);
          }
        } else if (attempts >= maxAttempts) {
          clearInterval(checkInterval);
          if (!resolved) {
            resolved = true;
            router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
            resolve(false);
          }
        }
      }, 10);
    });
  }

  const canActivate = authService.isAuthenticated();
  if (!canActivate) {
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  }
  return canActivate;
};
