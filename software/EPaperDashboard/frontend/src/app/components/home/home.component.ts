import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [RouterModule],
  template: `
    <div class="izboard-hero bg-transition-300 d-flex align-items-start justify-content-center" style="min-height:100vh;padding-top:56px;">
      <div class="px-3" style="max-width:680px;width:100%;margin:0 auto;">
        <!-- Logo -->
        <div class="mb-4 text-center">
          <img src="/icon.svg" alt="izBoard Logo" class="hero-logo">
        </div>

        <!-- Headline -->
        <h1 class="fw-bold hero-title mb-3 text-center" style="font-size:3rem;letter-spacing:-1px;line-height:1.2;margin-bottom:0.85rem !important;">
          izBoard
        </h1>

        <!-- Subtitle -->
        <p class="hero-subtitle text-center mb-4" style="font-size:1.15rem;margin-bottom:1.5rem !important;line-height:1.45;color:var(--bs-secondary-color);max-width:520px;margin-left:auto;margin-right:auto;">
          Bring Home Assistant dashboards to an E‑Paper display
        </p>

        <!-- Core features -->
        <div class="hero-features mb-4" style="margin-bottom:1.5rem !important;max-width:620px;margin-left:auto;margin-right:auto;display:flex;flex-direction:column;gap:8px;">
          <div class="feature-item" style="display:flex;gap:0.75rem;align-items:flex-start;justify-content:flex-start;padding:12px 14px;border:1px solid rgba(128,128,128,0.3);border-radius:12px;background:rgba(128,128,128,0.1);text-align:center;transition:all 0.2s ease;box-shadow:0 2px 4px rgba(0,0,0,0.08);cursor:default;">
            <i class="fa-solid fa-house" style="font-size:1.15rem;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="font-size:1rem;line-height:1.6;color:var(--bs-body-color);text-align:left;flex:1;">Supports Home Assistant dashboard views</div>
          </div>
          <div class="feature-item" style="display:flex;gap:0.75rem;align-items:flex-start;justify-content:flex-start;padding:12px 14px;border:1px solid rgba(128,128,128,0.3);border-radius:12px;background:rgba(128,128,128,0.1);text-align:center;transition:all 0.2s ease;box-shadow:0 2px 4px rgba(0,0,0,0.08);cursor:default;">
            <i class="fa-solid fa-microchip" style="font-size:1.15rem;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="font-size:1rem;line-height:1.6;color:var(--bs-body-color);text-align:left;flex:1;">An ESP32-based device pulls rendered images on a schedule</div>
          </div>
          <div class="feature-item" style="display:flex;gap:0.75rem;align-items:flex-start;justify-content:flex-start;padding:12px 14px;border:1px solid rgba(128,128,128,0.3);border-radius:12px;background:rgba(128,128,128,0.1);text-align:center;transition:all 0.2s ease;box-shadow:0 2px 4px rgba(0,0,0,0.08);cursor:default;">
            <i class="fa-solid fa-globe" style="font-size:1.15rem;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="font-size:1rem;line-height:1.6;color:var(--bs-body-color);text-align:left;flex:1;">Web application to configure the device and schedule updates</div>
          </div>
          <div class="feature-item" style="display:flex;gap:0.75rem;align-items:flex-start;justify-content:flex-start;padding:12px 14px;border:1px solid rgba(128,128,128,0.3);border-radius:12px;background:rgba(128,128,128,0.1);text-align:center;transition:all 0.2s ease;box-shadow:0 2px 4px rgba(0,0,0,0.08);cursor:default;">
            <i class="fa-solid fa-battery-three-quarters" style="font-size:1.15rem;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="font-size:1rem;line-height:1.6;color:var(--bs-body-color);text-align:left;flex:1;">Long battery life – days to weeks depending on update schedule</div>
          </div>
          <div class="feature-item" style="display:flex;gap:0.75rem;align-items:flex-start;justify-content:flex-start;padding:12px 14px;border:1px solid rgba(128,128,128,0.3);border-radius:12px;background:rgba(128,128,128,0.1);text-align:center;transition:all 0.2s ease;box-shadow:0 2px 4px rgba(0,0,0,0.08);cursor:default;">
            <i class="fa-solid fa-table-columns" style="font-size:1.15rem;color:var(--bs-primary);flex-shrink:0;margin-top:0.15rem;"></i>
            <div style="font-size:1rem;line-height:1.6;color:var(--bs-body-color);text-align:left;flex:1;">7.5" Black/White/Red E‑Paper display</div>
          </div>
        </div>

        <!-- CTA Buttons -->
        <div class="d-flex gap-3 justify-content-center flex-wrap">
          <a class="btn btn-primary btn-lg px-5 hero-btn-primary" routerLink="/dashboards" style="font-weight:600;font-size:1.05rem;padding:0.75rem 2.25rem !important;">
            Get Started
          </a>
          <a class="btn btn-outline-primary btn-lg px-5 hero-btn-github" href="https://github.com/izdev-digital/e-paper-dashboard" target="_blank" style="font-weight:600;font-size:1.05rem;padding:0.75rem 2.25rem !important;border-width:1.5px;">
            GitHub
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .izboard-hero {
      position:relative;
      overflow:hidden;
      background: var(--bs-body-bg);
    }
    
    /* Light mode - subtle neutral gradient */
    [data-bs-theme="light"] .izboard-hero {
      background: linear-gradient(135deg, #ffffff 0%, #f8f9fa 100%);
    }
    
    /* Dark mode - subtle dark gradient */
    [data-bs-theme="dark"] .izboard-hero {
      background: linear-gradient(135deg, #212529 0%, #1a1d20 100%);
    }
    
    .izboard-hero::before {
      content:'';
      position:absolute;
      top:0;
      left:0;
      right:0;
      bottom:0;
      pointer-events:none;
      opacity:0.4;
      transition: inherit;
    }
    
    /* Light mode - very subtle accent pattern */
    [data-bs-theme="light"] .izboard-hero::before {
      background-image:
        radial-gradient(circle at 20% 80%, rgba(108, 117, 125, 0.04) 0%, transparent 50%),
        radial-gradient(circle at 80% 20%, rgba(108, 117, 125, 0.04) 0%, transparent 50%);
    }
    
    /* Dark mode - slightly visible accent pattern */
    [data-bs-theme="dark"] .izboard-hero::before {
      background-image:
        radial-gradient(circle at 20% 80%, rgba(108, 117, 125, 0.08) 0%, transparent 50%),
        radial-gradient(circle at 80% 20%, rgba(108, 117, 125, 0.08) 0%, transparent 50%);
    }
    
    .izboard-hero > * {
      position:relative;
      z-index:1;
    }
    
    .feature-item:hover {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12) !important;
      border-color: rgba(128, 128, 128, 0.4) !important;
      background: rgba(128, 128, 128, 0.14) !important;
    }
    
    .feature-item:active {
      transform: translateY(0);
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08) !important;
    }
    
    .feature-item:active {
      transform: translateY(0);
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08) !important;
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
export class HomeComponent { }
