import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterModule],
  template: `
    <div class="welcome-epaper-dashboard d-flex flex-column align-items-center justify-content-center" style="min-height:60vh;">
      <img src="/icon.svg" alt="EPaper Dashboard Logo" style="width:240px;height:240px;margin-bottom:1rem;">
      <h1 class="display-3 mb-3 fw-bold">Welcome to EPaper Dashboard</h1>
      <p class="lead mb-4 text-secondary" style="max-width:600px;">
        Effortlessly manage your e-paper displays with a secure, user-friendly dashboard. <br />
        <span class="d-block mt-2">Create, edit, and control dashboards for your e-paper devices, all in one place.</span>
      </p>
      <div class="d-flex gap-3">
        <a class="btn btn-primary btn-lg px-4" routerLink="/dashboards">Go to Dashboards</a>
      </div>
    </div>
  `,
  styles: [`
    .welcome-epaper-dashboard h1 {
      letter-spacing: -1px;
    }
    .welcome-epaper-dashboard img {
      border-radius: 16px;
      box-shadow: 0 4px 24px rgba(0,0,0,0.08);
    }
  `]
})
export class HomeComponent {}
