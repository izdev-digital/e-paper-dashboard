const fs = require('fs');
const path = require('path');

const template = `<nav class="navbar navbar-expand-lg navbar-dark bg-dark">
  <div class="container-fluid">
    <a class="navbar-brand" routerLink="/home">izPanel</a>
    <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav">
      <span class="navbar-toggler-icon"></span>
    </button>
    <div class="collapse navbar-collapse" id="navbarNav">
      <ul class="navbar-nav me-auto">
        @if (authService.isAuthenticated) {
          <li class="nav-item">
            <a class="nav-link" routerLink="/dashboards" routerLinkActive="active">Dashboards</a>
          </li>
        }
      </ul>
      <ul class="navbar-nav">
        @if (authService.currentUser$ | async; as user) {
          <li class="nav-item">
            <span class="nav-link">Welcome, {{ user.username }}</span>
          </li>
          <li class="nav-item">
            <button class="btn btn-outline-light btn-sm ms-2" (click)="logout()">Logout</button>
          </li>
        } @else {
          <li class="nav-item">
            <a class="nav-link" routerLink="/login" routerLinkActive="active">Login</a>
          </li>
          <li class="nav-item">
            <a class="nav-link" routerLink="/register" routerLinkActive="active">Register</a>
          </li>
        }
      </ul>
    </div>
  </div>
</nav>

<div class="container mt-4">
  <router-outlet></router-outlet>
</div>
`;

const appHtmlPath = path.join(__dirname, 'src', 'app', 'app.html');
fs.writeFileSync(appHtmlPath, template, 'utf8');
console.log('app.html written successfully!');
