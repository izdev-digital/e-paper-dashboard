import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { 
    path: 'home', 
    loadComponent: () => import('./components/home/home.component').then(m => m.HomeComponent)
  },
  { 
    path: 'login', 
    loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent)
  },
  { 
    path: 'register', 
    loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent)
  },
  { 
    path: 'dashboards/create', 
    canActivate: [authGuard],
    loadComponent: () => import('./components/dashboard-create/dashboard-create.component').then(m => m.DashboardCreateComponent)
  },
  { 
    path: 'dashboards/:id/edit', 
    canActivate: [authGuard],
    loadComponent: () => import('./components/dashboard-edit/dashboard-edit.component').then(m => m.DashboardEditComponent)
  },
  { 
    path: 'dashboards', 
    canActivate: [authGuard],
    loadComponent: () => import('./components/dashboard-list/dashboard-list.component').then(m => m.DashboardListComponent)
  },
  { 
    path: 'users/profile', 
    canActivate: [authGuard],
    loadComponent: () => import('./components/profile/profile.component').then(m => m.ProfileComponent)
  },
  { path: '**', redirectTo: '/home' }
];
