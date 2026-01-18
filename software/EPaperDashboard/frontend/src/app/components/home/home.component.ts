import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterModule],
  template: `
    <div class="izpanel-hero d-flex align-items-start justify-content-center" style="min-height:100vh;padding-top:80px;">
      <div class="px-3" style="max-width:700px;width:100%;margin:0 auto;">
        <!-- Logo -->
        <div class="mb-6 text-center">
          <img src="/icon.svg" alt="izPanel Logo" class="hero-logo">
        </div>
        
        <!-- Headline -->
        <h1 class="fw-bold hero-title mb-3 text-center" style="font-size:3.5rem;letter-spacing:-1.5px;line-height:1.1;margin-bottom:2rem !important;">
          izPanel
        </h1>
        
        <!-- Subtitle -->
        <p class="hero-subtitle text-center mb-4" style="font-size:1.35rem;margin-bottom:3rem !important;font-weight:500;">
          E-Paper dashboards for Home Assistant
        </p>
        
        <!-- Description -->
        <div class="hero-features mb-5" style="margin-bottom:3rem !important;">
          <div style="display:flex;gap:1.2rem;margin-bottom:1.2rem;align-items:flex-start;">
            <i class="fa-solid fa-cog" style="width:1.5rem;height:1.5rem;display:flex;align-items:center;justify-content:center;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="flex:1;font-size:1.05rem;line-height:1.6;color:var(--bs-body-color);">Set up once, display dashboards everywhere</div>
          </div>
          <div style="display:flex;gap:1.2rem;margin-bottom:1.2rem;align-items:flex-start;">
            <i class="fa-solid fa-battery-full" style="width:1.5rem;height:1.5rem;display:flex;align-items:center;justify-content:center;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="flex:1;font-size:1.05rem;line-height:1.6;color:var(--bs-body-color);">Long battery life<span style="font-size:0.85rem;color:var(--bs-secondary-color);"> (depends on update schedule)</span></div>
          </div>
          <div style="display:flex;gap:1.2rem;margin-bottom:1.2rem;align-items:flex-start;">
            <i class="fa-solid fa-bolt" style="width:1.5rem;height:1.5rem;display:flex;align-items:center;justify-content:center;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="flex:1;font-size:1.05rem;line-height:1.6;color:var(--bs-body-color);">Real-time updates</div>
          </div>
          <div style="display:flex;gap:1.2rem;align-items:flex-start;">
            <i class="fa-solid fa-wrench" style="width:1.5rem;height:1.5rem;display:flex;align-items:center;justify-content:center;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="flex:1;font-size:1.05rem;line-height:1.6;color:var(--bs-body-color);">Minimal maintenance</div>
          </div>
        </div>
        
        <!-- CTA Buttons -->
        <div class="d-flex gap-3 justify-content-center flex-wrap" style="margin-bottom:1.5rem;">
          <a class="btn btn-primary btn-lg px-5 hero-btn-primary" routerLink="/dashboards" style="font-weight:600;font-size:1.1rem;padding:0.85rem 2.5rem !important;">
            Get Started
          </a>
          <a class="btn btn-outline-primary btn-lg px-5 hero-btn-github" href="https://github.com/izdev-digital/e-paper-dashboard" target="_blank" style="font-weight:600;font-size:1.1rem;padding:0.85rem 2.5rem !important;border-width:1.5px;">
            GitHub
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .izpanel-hero {
      position:relative;
      overflow:hidden;
      background: var(--bs-body-bg);
      transition: background-color 0.3s ease;
    }
    
    /* Light mode - subtle neutral gradient */
    [data-bs-theme="light"] .izpanel-hero {
      background: linear-gradient(135deg, #ffffff 0%, #f8f9fa 100%);
    }
    
    /* Dark mode - subtle dark gradient */
    [data-bs-theme="dark"] .izpanel-hero {
      background: linear-gradient(135deg, #212529 0%, #1a1d20 100%);
    }
    
    .izpanel-hero::before {
      content:'';
      position:absolute;
      top:0;
      left:0;
      right:0;
      bottom:0;
      pointer-events:none;
      opacity:0.4;
    }
    
    /* Light mode - very subtle accent pattern */
    [data-bs-theme="light"] .izpanel-hero::before {
      background-image:
        radial-gradient(circle at 20% 80%, rgba(108, 117, 125, 0.04) 0%, transparent 50%),
        radial-gradient(circle at 80% 20%, rgba(108, 117, 125, 0.04) 0%, transparent 50%);
    }
    
    /* Dark mode - slightly visible accent pattern */
    [data-bs-theme="dark"] .izpanel-hero::before {
      background-image:
        radial-gradient(circle at 20% 80%, rgba(108, 117, 125, 0.08) 0%, transparent 50%),
        radial-gradient(circle at 80% 20%, rgba(108, 117, 125, 0.08) 0%, transparent 50%);
    }
    
    .izpanel-hero > * {
      position:relative;
      z-index:1;
    }
    
    .hero-title {
      color: var(--bs-body-color);
    }
    
    .hero-subtitle {
      color: var(--bs-body-color);
      font-weight: 500;
    }
    
    .hero-description {
      color: var(--bs-secondary-color);
    }
    
    .hero-logo {
      width:160px;
      height:160px;
      border-radius:20px;
      transition: box-shadow 0.3s ease, transform 0.3s ease;
    }
    
    .hero-logo:hover {
      transform: scale(1.03);
    }
    
    [data-bs-theme="light"] .hero-logo {
      box-shadow: 0 16px 56px rgba(0, 0, 0, 0.12);
    }
    
    [data-bs-theme="dark"] .hero-logo {
      box-shadow: 0 16px 56px rgba(0, 0, 0, 0.5);
    }
    
    .hero-btn-primary,
    .hero-btn-github {
      transition: all 0.2s ease;
    }
    
    .hero-btn-primary:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.15);
    }
    
    .hero-btn-github:hover {
      transform: translateY(-2px);
    }
    
    .btn {
      transition:all 0.3s cubic-bezier(0.4,0,0.2,1);
      text-decoration:none !important;
    }
    
    .btn:hover {
      transform:translateY(-2px);
    }
    
    [data-bs-theme="light"] .btn-primary:hover {
      box-shadow:0 12px 24px rgba(13, 110, 253, 0.25);
    }
    
    [data-bs-theme="dark"] .btn-primary:hover {
      box-shadow:0 12px 24px rgba(13, 110, 253, 0.4);
    }
    
    [data-bs-theme="light"] .btn-outline-primary:hover {
      box-shadow:0 12px 24px rgba(13, 110, 253, 0.15);
    }
    
    [data-bs-theme="dark"] .btn-outline-primary:hover {
      box-shadow:0 12px 24px rgba(13, 110, 253, 0.3);
    }
    
    @media (max-width:768px) {
      .display-3 {
        font-size:2rem;
      }
      .hero-subtitle {
        font-size:1.1rem !important;
      }
      .btn-lg {
        font-size:1rem !important;
        padding: 0.5rem 1.5rem !important;
      }
      .hero-logo {
        width:100px;
        height:100px;
      }
    }
  `]
})
export class HomeComponent {}
