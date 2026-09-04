import { Routes } from '@angular/router';
import { authGuard, guestGuard, profileGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    data: { mode: 'login' },
    loadComponent: () => import('./features/auth/auth-page').then((module) => module.AuthPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    data: { mode: 'register' },
    loadComponent: () => import('./features/auth/auth-page').then((module) => module.AuthPage),
  },
  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () => import('./features/onboarding/onboarding-page').then((module) => module.OnboardingPage),
  },
  {
    path: 'lobby',
    canActivate: [profileGuard],
    loadComponent: () => import('./features/lobby/lobby').then((module) => module.Lobby),
  },
  {
    path: 'game',
    canActivate: [profileGuard],
    loadComponent: () =>
      import('./features/game/game.component').then((module) => module.GameComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'lobby' },
  { path: '**', redirectTo: 'lobby' },
];
