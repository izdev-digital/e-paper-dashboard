import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { superUserGuard } from './guards/superuser.guard';

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
    path: 'dashboards/:id/designer',
    canActivate: [authGuard],
    loadComponent: () => import('./components/dashboard-designer/dashboard-designer.component').then(m => m.DashboardDesignerComponent)
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
  {
    path: 'users/manage',
    canActivate: [authGuard, superUserGuard],
    loadComponent: () => import('./components/users-management/users-management.component').then(m => m.UsersManagementComponent)
  },
  {
    path: 'privacy',
    loadComponent: () => import('./components/privacy/privacy.component').then(m => m.PrivacyComponent)
  },
  { path: '**', redirectTo: '/home' }
];
