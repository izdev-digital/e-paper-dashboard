import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterModule],
  template: `
    <div class="izpanel-hero d-flex flex-column justify-content-between" style="min-height:100vh;background:linear-gradient(135deg,#0d6efd 0%,#0055cc 100%);">
      <!-- Main Content -->
      <div class="d-flex flex-column align-items-center justify-content-center flex-grow-1 px-3 py-5">
        <div class="text-center">
          <!-- Logo -->
          <div class="mb-5">
            <img src="/icon.svg" alt="izPanel Logo" class="hero-logo">
          </div>
          
          <!-- Headline -->
          <h1 class="display-2 fw-bold text-white mb-3" style="letter-spacing:-2px;line-height:1.1;">
            izPanel
          </h1>
          
          <!-- Tagline -->
          <p class="lead text-white mb-2" style="font-size:1.5rem;font-weight:500;opacity:0.95;">
            Configure Once, Leverage Every Day
          </p>
          
          <!-- Description -->
          <p class="text-white mb-5" style="max-width:700px;font-size:1.1rem;opacity:0.9;margin-left:auto;margin-right:auto;">
            Stunning E-Paper dashboards powered by Home Assistant. 
            Real-time updates. Weeks of battery life. Zero maintenance.
          </p>
          
          <!-- CTA Buttons -->
          <div class="d-flex gap-3 justify-content-center flex-wrap mb-4">
            <a class="btn btn-light btn-lg px-5" routerLink="/dashboards" style="font-weight:600;font-size:1.1rem;">
              ✓ Get Started
            </a>
            <a class="btn btn-outline-light btn-lg px-5" href="https://github.com" target="_blank" style="font-weight:600;font-size:1.1rem;border-width:2px;">
              Learn More
            </a>
          </div>
          
          <!-- Feature Pills -->
          <div class="d-flex gap-2 justify-content-center flex-wrap mt-5">
            <span class="badge bg-light text-primary px-3 py-2" style="font-size:0.95rem;">🔋 Weeks of Battery</span>
            <span class="badge bg-light text-primary px-3 py-2" style="font-size:0.95rem;">🏠 Home Assistant</span>
            <span class="badge bg-light text-primary px-3 py-2" style="font-size:0.95rem;">📊 Real-Time Updates</span>
            <span class="badge bg-light text-primary px-3 py-2" style="font-size:0.95rem;">🔐 Self-Hosted</span>
          </div>
        </div>
      </div>
      
      <!-- Footer -->
      <div class="px-3 py-4 text-center text-white" style="opacity:0.8;font-size:0.95rem;">
        <p class="mb-0">Part of the smart home ecosystem • Open source • Made with ❤️</p>
      </div>
    </div>
  `,
  styles: [`
    .izpanel-hero {
      background:linear-gradient(135deg,#0d6efd 0%,#0055cc 100%);
      position:relative;
      overflow:hidden;
    }
    
    .izpanel-hero::before {
      content:'';
      position:absolute;
      top:0;
      left:0;
      right:0;
      bottom:0;
      background-image:
        radial-gradient(circle at 20% 80%,rgba(255,255,255,0.1) 0%,transparent 50%),
        radial-gradient(circle at 80% 20%,rgba(255,255,255,0.1) 0%,transparent 50%);
      pointer-events:none;
    }
    
    .izpanel-hero > * {
      position:relative;
      z-index:1;
    }
    
    .hero-logo {
      width:200px;
      height:200px;
      border-radius:24px;
      box-shadow:0 20px 60px rgba(0,0,0,0.3);
      animation:logoFloat 3s ease-in-out infinite;
    }
    
    @keyframes logoFloat {
      0%, 100% {
        transform:translateY(0px);
      }
      50% {
        transform:translateY(-20px);
      }
    }
    
    .btn {
      transition:all 0.3s cubic-bezier(0.4,0,0.2,1);
      text-decoration:none !important;
    }
    
    .btn-light:hover {
      transform:translateY(-2px);
      box-shadow:0 12px 24px rgba(0,0,0,0.2);
      background-color:#fff !important;
    }
    
    .btn-outline-light:hover {
      transform:translateY(-2px);
      box-shadow:0 12px 24px rgba(255,255,255,0.2);
      background-color:rgba(255,255,255,0.15) !important;
    }
    
    .badge {
      font-weight:500;
      transition:all 0.2s ease;
      cursor:default;
    }
    
    .badge:hover {
      transform:translateY(-2px);
    }
    
    @media (max-width:768px) {
      .display-2 {
        font-size:2rem;
      }
      .lead {
        font-size:1.2rem !important;
      }
      .btn-lg {
        font-size:1rem !important;
      }
      .hero-logo {
        width:160px;
        height:160px;
      }
    }
  `]
})
export class HomeComponent {}
